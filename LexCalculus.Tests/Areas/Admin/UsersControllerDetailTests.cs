using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class UsersControllerDetailTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public UsersControllerDetailTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(int userId, bool allowAutoRedirect = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        }).WithAdminHeaders(userId);

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private async Task<ApplicationUser> SeedAdminAsync(string email)
    {
        await RemoveUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Admin Detail Test",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };
        var create = await um.CreateAsync(user, "ValidPass123!");
        create.Succeeded.Should().BeTrue();
        var addRole = await um.AddToRoleAsync(user, "Admin");
        addRole.Succeeded.Should().BeTrue();
        return user;
    }

    private async Task RemoveUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return;
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        ctx.UserRoles.RemoveRange(ctx.UserRoles.Where(ur => ur.UserId == user.Id));
        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Detail_AsAdmin_ReturnsUserInfo()
    {
        var admin = await SeedAdminAsync("detail-admin@example.com");

        var client = CreateAdminClient(admin.Id, allowAutoRedirect: true);
        var response = await client.GetAsync($"/admin/kullanicilar/{admin.Id}");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("detail-admin@example.com");
        body.Should().Contain("Aktif");
        body.Should().Contain("admin-detail");  // detail dl class

        await RemoveUserAsync("detail-admin@example.com");
    }

    [Fact]
    public async Task Pasiflestir_Self_BlockedByGuard()
    {
        var admin = await SeedAdminAsync("self-pasif@example.com");

        var client = CreateAdminClient(admin.Id, allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, $"/admin/kullanicilar/{admin.Id}");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });

        var response = await client.PostAsync($"/admin/kullanicilar/{admin.Id}/pasiflestir", form);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        // DB'de hâlâ aktif (self-guard çalıştı)
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.Users.FirstAsync(u => u.Id == admin.Id);
            refreshed.IsActive.Should().BeTrue("self-deactivate koruması engellemeli");
        }

        await RemoveUserAsync("self-pasif@example.com");
    }

    [Fact]
    public async Task Pasiflestir_OtherUser_DeactivatesThemAndRedirects()
    {
        var admin = await SeedAdminAsync("admin-actor@example.com");

        // Hedef kullanıcı (Kullanici rolünde, IsActive=true)
        ApplicationUser target;
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            target = new ApplicationUser
            {
                UserName = "victim@example.com",
                Email = "victim@example.com",
                FullName = "Victim",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                EmailConfirmed = true
            };
            (await um.CreateAsync(target, "ValidPass123!")).Succeeded.Should().BeTrue();
            await um.AddToRoleAsync(target, "Kullanici");
        }

        var client = CreateAdminClient(admin.Id, allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, $"/admin/kullanicilar/{target.Id}");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });

        var response = await client.PostAsync($"/admin/kullanicilar/{target.Id}/pasiflestir", form);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.Users.FirstAsync(u => u.Id == target.Id);
            refreshed.IsActive.Should().BeFalse();
        }

        await RemoveUserAsync("admin-actor@example.com");
        await RemoveUserAsync("victim@example.com");
    }
}

internal static class AdminClientExtensions
{
    public static HttpClient WithAdminHeaders(this HttpClient client, int userId)
    {
        client.DefaultRequestHeaders.Add("X-Test-User", $"admin-test-{userId}@local");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }
}
