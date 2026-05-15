using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Social;

[Collection("AdminWebHost")]
public class BaglantilarimPageTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public BaglantilarimPageTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthClient(int userId, string email, bool allowAutoRedirect = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
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

    private async Task<ApplicationUser> SeedUserAsync(string email, string fullName)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(user, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = fullName, PublicSlug = $"slug-{Guid.NewGuid():N}".Substring(0, 20)
        });
        await ctx.SaveChangesAsync();
        return user;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;

        var conns = await ctx.UserConnections
            .Where(c => c.RequesterId == u.Id || c.TargetId == u.Id)
            .ToListAsync();
        ctx.UserConnections.RemoveRange(conns);

        var blocks = await ctx.UserBlocks
            .Where(b => b.BlockerId == u.Id || b.BlockedId == u.Id)
            .ToListAsync();
        ctx.UserBlocks.RemoveRange(blocks);

        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);

        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);

        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private async Task<int> SeedConnectionAsync(int requesterId, int targetId, UserConnectionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new UserConnection
        {
            RequesterId = requesterId,
            TargetId = targetId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            RespondedAt = status == UserConnectionStatus.Accepted ? DateTime.UtcNow : null
        };
        ctx.UserConnections.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task OnGet_AnonymousUser_RejectsWithChallenge()
    {
        // Production'da cookie auth → 302 /Identity/Account/Login redirect.
        // Test ortamında (SqlServerTestAuthWebApplicationFactory) TestAuthHandler default
        // challenge scheme'i ve HandleChallengeAsync 401 döner. Her iki durum da
        // [Authorize] korumasının çalıştığını gösterir.
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/baglantilarim");
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task OnGet_DefaultTab_ReturnsActive()
    {
        var u1 = await SeedUserAsync("bag-active-1@example.com", "Active 1");
        var u2 = await SeedUserAsync("bag-active-2@example.com", "Active 2");
        try
        {
            await SeedConnectionAsync(u1.Id, u2.Id, UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(u1.Id, u1.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/baglantilarim");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Active 2", "aktif sekmedeki bağlı kullanıcı görünmeli");
            body.Should().Contain("Bağlantılarım");
            body.Should().Contain("baglantilarim__tab--active",
                "default tab=aktif vurgulanmış olmalı");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }

    [Fact]
    public async Task OnGet_BekleyenTab_ReturnsPendingForUser()
    {
        var me = await SeedUserAsync("bag-target@example.com", "Hedef Kullanıcı");
        var sender = await SeedUserAsync("bag-sender@example.com", "Gönderen Kullanıcı");
        try
        {
            await SeedConnectionAsync(sender.Id, me.Id, UserConnectionStatus.Pending);

            using var client = CreateAuthClient(me.Id, me.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/baglantilarim?tab=bekleyen");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("nderen Kullan", "isim render olmalı (HTML encode olabilir)");
            body.Should().Contain("Kabul Et");
            body.Should().Contain("Reddet");
        }
        finally
        {
            await CleanupUserAsync(me.Email!);
            await CleanupUserAsync(sender.Email!);
        }
    }

    [Fact]
    public async Task OnGet_GonderdiklerimTab_ReturnsSentByUser()
    {
        var me = await SeedUserAsync("bag-requester@example.com", "İstek Sahibi");
        var other = await SeedUserAsync("bag-other@example.com", "Diğer Kullanıcı");
        try
        {
            await SeedConnectionAsync(me.Id, other.Id, UserConnectionStatus.Pending);

            using var client = CreateAuthClient(me.Id, me.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/baglantilarim?tab=gonderdiklerim");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("er Kullan", "diğer kullanıcı (HTML encode için kısaltıldı)");
            body.Should().Contain("İptal Et").And.NotContain("Kabul Et");
        }
        finally
        {
            await CleanupUserAsync(me.Email!);
            await CleanupUserAsync(other.Email!);
        }
    }

    [Fact]
    public async Task OnPostAccept_NonTargetUser_ReturnsError()
    {
        var sender = await SeedUserAsync("bag-np-sender@example.com", "Sender");
        var target = await SeedUserAsync("bag-np-target@example.com", "Target");
        var thirdParty = await SeedUserAsync("bag-np-third@example.com", "Üçüncü");
        try
        {
            var connId = await SeedConnectionAsync(sender.Id, target.Id, UserConnectionStatus.Pending);

            using var client = CreateAuthClient(thirdParty.Id, thirdParty.Email!, allowAutoRedirect: false);
            // 3. kullanıcının /baglantilarim sayfasında form yok (boş liste); antiforgery
            // token formdan geliyor. /profil her zaman form içerir, aynı cookie + token
            // çifti POST /baglantilarim için de geçerli.
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", connId.ToString())
            });
            var response = await client.PostAsync("/baglantilarim?handler=Accept", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
                "PRG: handler her zaman redirect döner");

            // DB'de hâlâ Pending olmalı (3. kullanıcı yetkili değil)
            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conn = await ctx.UserConnections.AsNoTracking().FirstAsync(c => c.Id == connId);
            conn.Status.Should().Be(UserConnectionStatus.Pending);
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(target.Email!);
            await CleanupUserAsync(thirdParty.Email!);
        }
    }

    [Fact]
    public async Task OnPostAccept_ValidPending_SucceedsAndPersistsAccepted()
    {
        var sender = await SeedUserAsync("bag-ap-sender@example.com", "Sender");
        var target = await SeedUserAsync("bag-ap-target@example.com", "Target");
        try
        {
            var connId = await SeedConnectionAsync(sender.Id, target.Id, UserConnectionStatus.Pending);

            using var client = CreateAuthClient(target.Id, target.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/baglantilarim?tab=bekleyen");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", connId.ToString())
            });
            var response = await client.PostAsync("/baglantilarim?handler=Accept", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().Should().Contain("tab=bekleyen");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conn = await ctx.UserConnections.AsNoTracking().FirstAsync(c => c.Id == connId);
            conn.Status.Should().Be(UserConnectionStatus.Accepted);
            conn.RespondedAt.Should().NotBeNull();
        }
        finally
        {
            await CleanupUserAsync(sender.Email!);
            await CleanupUserAsync(target.Email!);
        }
    }

    [Fact]
    public async Task OnPostRemove_ActiveConnection_HardDeletes()
    {
        var u1 = await SeedUserAsync("bag-rm-1@example.com", "Kaldırıcı");
        var u2 = await SeedUserAsync("bag-rm-2@example.com", "Diğer Taraf");
        try
        {
            var connId = await SeedConnectionAsync(u1.Id, u2.Id, UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(u1.Id, u1.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/baglantilarim");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", connId.ToString())
            });
            var response = await client.PostAsync("/baglantilarim?handler=Remove", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await ctx.UserConnections.AnyAsync(c => c.Id == connId);
            exists.Should().BeFalse("Remove hard delete olmalı");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }

    // Faz 4.3 — Engellenenler sekmesi + Unblock handler

    private async Task SeedBlockAsync(int blockerId, int blockedId)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.UserBlocks.Add(new UserBlock
        {
            BlockerId = blockerId, BlockedId = blockedId,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task OnGet_EngellenenlerTab_ListsBlockedUsers()
    {
        var u1 = await SeedUserAsync("bag-eng-1@example.com", "Engelleyen");
        var u2 = await SeedUserAsync("bag-eng-2@example.com", "Engellenen");
        try
        {
            await SeedBlockAsync(u1.Id, u2.Id);

            using var client = CreateAuthClient(u1.Id, u1.Email!);
            var response = await client.GetAsync("/baglantilarim?tab=engellenenler");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Engellenenler");
            body.Should().Contain("Engellenen", "kart'ta engellenen kullanıcı görünmeli");
            body.Should().Contain("Engellemeyi Kald", "Unblock butonu");
            body.Should().Contain("baglantilarim__tab--active",
                "engellenenler tab aktif");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }

    [Fact]
    public async Task OnGet_EngellenenlerTab_NoBlocks_ShowsEmptyMessage()
    {
        var u1 = await SeedUserAsync("bag-eng-empty@example.com", "Hic Engellemedi");
        try
        {
            using var client = CreateAuthClient(u1.Id, u1.Email!);
            var response = await client.GetAsync("/baglantilarim?tab=engellenenler");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Engellediğiniz");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
        }
    }

    [Fact]
    public async Task OnPostUnblock_Valid_RemovesBlockAndRedirects()
    {
        var u1 = await SeedUserAsync("bag-unblock-1@example.com", "Engelleyen");
        var u2 = await SeedUserAsync("bag-unblock-2@example.com", "Engellenen");
        try
        {
            await SeedBlockAsync(u1.Id, u2.Id);

            using var client = CreateAuthClient(u1.Id, u1.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/baglantilarim?tab=engellenenler");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("blockedUserId", u2.Id.ToString())
            });
            var response = await client.PostAsync("/baglantilarim?handler=Unblock", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserBlocks.AnyAsync(b => b.BlockerId == u1.Id && b.BlockedId == u2.Id))
                .Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }
}
