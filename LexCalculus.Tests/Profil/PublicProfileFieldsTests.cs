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

namespace LexCalculus.Tests.Profil;

[Collection("AdminWebHost")]
public class PublicProfileFieldsTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public PublicProfileFieldsTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId, bool allowAutoRedirect = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", $"public-profile-{userId}@local");
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

    private async Task<ApplicationUser> CreateUserAsync(
        string email, string fullName, int? tenantId = null)
    {
        await RemoveUserIfExistsAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenantId
        };
        var result = await um.CreateAsync(user, "ValidPass123!");
        result.Succeeded.Should().BeTrue();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            DisplayName = fullName
        });
        await ctx.SaveChangesAsync();

        return user;
    }

    private async Task RemoveUserIfExistsAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return;
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == user.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
    }

    private static List<KeyValuePair<string, string>> BaseForm(
        string token, string fullName,
        bool isPublic = false,
        bool showTenant = false,
        string? bio = null,
        string? city = null)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Input.FullName", fullName)
        };
        if (isPublic) form.Add(new("Input.IsPublicProfile", "true"));
        if (showTenant) form.Add(new("Input.ShowTenant", "true"));
        if (bio is not null) form.Add(new("Input.Bio", bio));
        if (city is not null) form.Add(new("Input.City", city));
        return form;
    }

    [Fact]
    public async Task OnPost_SetsIsPublicProfile_TogglePersistsAndKeepsBioCity()
    {
        var user = await CreateUserAsync("pp-toggle@example.com", "Toggle Tester");
        try
        {
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Toggle Tester",
                    isPublic: true, bio: "Merhaba", city: "İstanbul")));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.IsPublicProfile.Should().BeTrue();
            profile.Bio.Should().Be("Merhaba");
            profile.City.Should().Be("İstanbul");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-toggle@example.com");
        }
    }

    [Fact]
    public async Task OnPost_DefensiveFallback_GeneratesSlugWhenProfileSlugIsNull()
    {
        // Eski kayıt simülasyonu — PublicSlug null bir profile, kullanıcı IsPublicProfile=true
        // post yapınca defansif fallback DisplayName'den slug üretmeli.
        var user = await CreateUserAsync("pp-fallback@example.com", "Fallback Avukat");
        try
        {
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Fallback Avukat", isPublic: true)));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.PublicSlug.Should().NotBeNullOrEmpty(
                "OnPostAsync defansif fallback null slug için DisplayName tabanlı üretir");
            profile.PublicSlug.Should().MatchRegex("^[a-z0-9-]+$");
            profile.PublicSlug.Should().Contain("fallback");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-fallback@example.com");
        }
    }

    [Fact]
    public async Task OnPost_PreservesSlugWhenProfileBecomesPrivate()
    {
        var user = await CreateUserAsync("pp-preserve@example.com", "Preserve Tester");
        try
        {
            // 1) Public + slug üret
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var p = await ctx.UserProfiles.FirstAsync(x => x.UserId == user.Id);
                p.IsPublicProfile = true;
                p.PublicSlug = "preserve-test";
                await ctx.SaveChangesAsync();
            }

            // 2) Profil gizli yap (IsPublicProfile=false post)
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");
            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Preserve Tester", isPublic: false)));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            // Slug korundu mu? (OnPost slug'a dokunmaz, sadece null fallback yapar)
            using var scope2 = _factory.Services.CreateScope();
            var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx2.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.IsPublicProfile.Should().BeFalse();
            profile.PublicSlug.Should().Be("preserve-test");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-preserve@example.com");
        }
    }

    [Fact]
    public async Task OnPost_ShowTenantIgnoredWhenUserHasNoTenant()
    {
        var user = await CreateUserAsync("pp-no-tenant@example.com", "No Tenant User",
            tenantId: null);
        try
        {
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            // ShowTenant=true post — defansif olarak DB'ye false yazılmalı
            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "No Tenant User",
                    isPublic: true, showTenant: true)));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.ShowTenant.Should().BeFalse(
                "tenant'sız kullanıcı için ShowTenant defansif olarak false yazılır");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-no-tenant@example.com");
        }
    }
}
