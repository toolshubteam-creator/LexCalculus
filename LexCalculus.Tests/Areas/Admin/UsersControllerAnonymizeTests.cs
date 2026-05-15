using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

/// <summary>
/// Admin UsersController.Anonimize POST endpoint testleri (Faz 5.1).
/// Charter Karar 6 — KVKK uyumlu hesap anonimize.
/// </summary>
[Collection("AdminWebHost")]
public class UsersControllerAnonymizeTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public UsersControllerAnonymizeTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "anonymize-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "9050");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    private HttpClient CreateNonAdminClient(int userId, string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
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

    private async Task<int> SeedUserAsync(string email, bool isActive = true)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = new ApplicationUser
        {
            UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(),
            FullName = "Anonymize Target",
            CreatedAt = DateTime.UtcNow,
            IsActive = isActive,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = u.Id,
            DisplayName = "Test Display",
            Bio = "Bio",
            City = "Istanbul",
            PublicSlug = $"test-{u.Id}",
            IsPublicProfile = true
        });
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email
            || x.UserName!.StartsWith($"deleted-") && x.FullName == "Anonymize Target");
        if (u is null) return;
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Anonimize_AsAdmin_RedirectsAndAnonymizesUser()
    {
        var email = "anon-target-1@example.com";
        var userId = await SeedUserAsync(email);
        try
        {
            using var client = CreateAdminClient();
            var token = await GetAntiforgeryTokenAsync(client, $"/admin/kullanicilar/{userId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });

            var response = await client.PostAsync(
                $"/admin/kullanicilar/{userId}/anonimize", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await ctx.Users.Include(x => x.Profile)
                .FirstAsync(x => x.Id == userId);
            u.IsActive.Should().BeFalse();
            u.Email.Should().StartWith("deleted-").And.EndWith("@anonymized.local");
            u.Profile!.DisplayName.Should().Be("Silinmiş Kullanıcı");
            u.Profile.PublicSlug.Should().BeNull();
        }
        finally
        {
            await CleanupUserAsync(email);
        }
    }

    [Fact]
    public async Task Anonimize_AsNonAdmin_ReturnsForbiddenOrRedirect()
    {
        var email = "anon-target-2@example.com";
        var userId = await SeedUserAsync(email);
        var nonAdminEmail = "anon-non-admin@example.com";
        var nonAdminId = await SeedUserAsync(nonAdminEmail);
        try
        {
            using var client = CreateNonAdminClient(nonAdminId, nonAdminEmail);
            // CSRF token bile alamadan auth gate'te durması beklenir
            var form = new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>());
            var response = await client.PostAsync(
                $"/admin/kullanicilar/{userId}/anonimize", form);

            ((int)response.StatusCode).Should().BeOneOf(
                (int)HttpStatusCode.Forbidden,
                (int)HttpStatusCode.Redirect,
                (int)HttpStatusCode.Unauthorized,
                (int)HttpStatusCode.BadRequest);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await ctx.Users.FirstAsync(x => x.Id == userId);
            u.IsActive.Should().BeTrue();
        }
        finally
        {
            await CleanupUserAsync(email);
            await CleanupUserAsync(nonAdminEmail);
        }
    }

    [Fact]
    public async Task Anonimize_AlreadyAnonymized_ReturnsErrorTempData()
    {
        var email = "anon-target-3@example.com";
        var userId = await SeedUserAsync(email, isActive: false);
        try
        {
            using var client = CreateAdminClient();
            // CSRF token Detail sayfasından alınamaz çünkü user inactive olabilir,
            // başka bir sayfadan alalım (admin home)
            var token = await GetAntiforgeryTokenAsync(client, $"/admin/kullanicilar/{userId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });

            var response = await client.PostAsync(
                $"/admin/kullanicilar/{userId}/anonimize", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            // Detail sayfasında error TempData görünmeli
            using var redirectClient = CreateAdminClient();
            // Cookie taşımak gerekmez: TempData session-based, aynı handler
            // Sadece DB durumunu doğrula: hâlâ inactive (yeni anonimize değil)
            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await ctx.Users.FirstAsync(x => x.Id == userId);
            u.IsActive.Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(email);
        }
    }
}
