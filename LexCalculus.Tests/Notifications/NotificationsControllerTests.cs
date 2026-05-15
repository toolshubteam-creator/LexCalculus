using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Notifications;

[Collection("AdminWebHost")]
public class NotificationsControllerTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public NotificationsControllerTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // SQL Server FK_Notifications_AspNetUsers_UserId zorunlu. IClassFixture
    // ile DB testler arası paylaşılıyor — user'ı email üzerinden
    // idempotent seed et, EF'in atadığı Id'yi kullan.
    private async Task<int> EnsureUserAsync(string emailSuffix)
    {
        var email = $"notif-{emailSuffix}@x.com";
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null) return existing.Id;

        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = $"Notif {emailSuffix}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task ClearAndSeedNotificationsAsync(params Notification[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Notifications.RemoveRange(ctx.Notifications.IgnoreQueryFilters());
        await ctx.SaveChangesAsync();
        if (rows.Length > 0)
        {
            ctx.Notifications.AddRange(rows);
            await ctx.SaveChangesAsync();
        }
    }

    private HttpClient CreateUserClient(int? actAsUserId = null, bool authenticated = true, bool allowAutoRedirect = false)
    {
        var options = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        };
        var client = _factory.CreateClient(options);
        if (authenticated)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");
            if (actAsUserId.HasValue)
                client.DefaultRequestHeaders.Add("X-Test-UserId", actAsUserId.Value.ToString());
        }
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task Sayim_Anonymous_Returns401()
    {
        await ClearAndSeedNotificationsAsync();
        var client = CreateUserClient(authenticated: false);

        var response = await client.GetAsync("/bildirimler/sayim");

        ((int)response.StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task Sayim_AsUser_ReturnsJsonWithUnreadCount()
    {
        var userId = await EnsureUserAsync("sayim");
        await ClearAndSeedNotificationsAsync(
            new Notification { UserId = userId, Type = NotificationType.SystemAlert,
                Title = "A", Body = "A", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = userId, Type = NotificationType.SystemAlert,
                Title = "B", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = userId, Type = NotificationType.SystemAlert,
                Title = "C", Body = "C", IsRead = true, ReadAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow });

        var client = CreateUserClient(actAsUserId: userId);
        var response = await client.GetAsync("/bildirimler/sayim");

        response.IsSuccessStatusCode.Should().BeTrue();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"unreadCount\":2");
    }

    [Fact]
    public async Task Index_AsUser_ReturnsList_WithSummary()
    {
        var userId = await EnsureUserAsync("index");
        await ClearAndSeedNotificationsAsync(
            new Notification { UserId = userId, Type = NotificationType.DataFreshness,
                Title = "Stale", Body = "Stale body", IsRead = false, CreatedAt = DateTime.UtcNow }
        );

        var client = CreateUserClient(actAsUserId: userId, allowAutoRedirect: true);
        var response = await client.GetAsync("/bildirimler");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Bildirimler");
        body.Should().Contain("okunmamış");
        body.Should().Contain("Stale");
    }

    [Fact]
    public async Task Oku_AsUser_MarksOwnNotificationAsRead()
    {
        var userId = await EnsureUserAsync("oku");
        var notif = new Notification
        {
            UserId = userId,
            Type = NotificationType.SystemAlert,
            Title = "T",
            Body = "B",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        await ClearAndSeedNotificationsAsync(notif);

        var client = CreateUserClient(actAsUserId: userId);
        var token = await GetAntiforgeryTokenAsync(client, "/bildirimler");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });
        var response = await client.PostAsync($"/bildirimler/oku/{notif.Id}", content);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        // DB doğrulama
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refreshed = await ctx.Notifications.FirstAsync(n => n.Id == notif.Id);
        refreshed.IsRead.Should().BeTrue();
        refreshed.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Oku_AsUser_DifferentOwnersNotification_Returns404()
    {
        // Bildirim user "other"e ait, ama biz user "self" olarak login.
        // self user'ın bildirimi de olsun ki /bildirimler sayfası antiforgery
        // token'lı form render etsin (boş sayfada token üretilmiyor).
        var selfId = await EnsureUserAsync("self");
        var otherId = await EnsureUserAsync("other");
        var notif = new Notification
        {
            UserId = otherId,
            Type = NotificationType.SystemAlert,
            Title = "Other",
            Body = "Body",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        var ownNotif = new Notification
        {
            UserId = selfId,
            Type = NotificationType.SystemAlert,
            Title = "Own",
            Body = "Own body",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        await ClearAndSeedNotificationsAsync(notif, ownNotif);

        var client = CreateUserClient(actAsUserId: selfId);
        var token = await GetAntiforgeryTokenAsync(client, "/bildirimler");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });
        var response = await client.PostAsync($"/bildirimler/oku/{notif.Id}", content);

        ((int)response.StatusCode).Should().Be(404);
    }
}
