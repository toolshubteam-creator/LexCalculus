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
public class PublicProfileFieldsTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public PublicProfileFieldsTests(TestAuthWebApplicationFactory factory)
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

    private async Task<int> SeedTenantAsync(string slug = "test-tenant")
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await ctx.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug);
        if (existing is not null) return existing.Id;

        // Sahte owner
        var owner = new ApplicationUser
        {
            UserName = $"owner-{slug}@x.com",
            Email = $"owner-{slug}@x.com",
            FullName = "Tenant Owner",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = "Test Tenant",
            Slug = slug,
            OwnerUserId = owner.Id,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();
        return tenant.Id;
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
        string? publicSlug = null,
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
        if (publicSlug is not null) form.Add(new("Input.PublicSlug", publicSlug));
        if (bio is not null) form.Add(new("Input.Bio", bio));
        if (city is not null) form.Add(new("Input.City", city));
        return form;
    }

    [Fact]
    public async Task OnPost_SetsIsPublicProfile_TogglePersists()
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
    public async Task OnPost_GeneratesSlugWhenEmpty_AndProfileIsPublic()
    {
        var user = await CreateUserAsync("pp-autoslug@example.com", "Otomatik Sluglu Avukat");
        try
        {
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            // PublicSlug verilmiyor, IsPublicProfile=true → otomatik üret
            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Otomatik Sluglu Avukat",
                    isPublic: true)));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.IsPublicProfile.Should().BeTrue();
            profile.PublicSlug.Should().NotBeNullOrEmpty();
            profile.PublicSlug.Should().MatchRegex("^[a-z0-9-]+$");
            profile.PublicSlug.Should().Contain("otomatik");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-autoslug@example.com");
        }
    }

    [Fact]
    public async Task OnPost_RejectsConflictingSlug_FromAnotherUser()
    {
        // İlk kullanıcı slug'ı kapar
        var first = await CreateUserAsync("pp-first@example.com", "First User");
        var second = await CreateUserAsync("pp-second@example.com", "Second User");
        try
        {
            // First public + slug
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var p = await ctx.UserProfiles.FirstAsync(x => x.UserId == first.Id);
                p.IsPublicProfile = true;
                p.PublicSlug = "rezerv-slug";
                await ctx.SaveChangesAsync();
            }

            // Second aynı slug'ı denesin
            var client = CreateAuthClient(second.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");
            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Second User",
                    isPublic: true, publicSlug: "rezerv-slug")));

            // Page() döner — 200 + hem validation summary hem field-altı span'da hata
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("kullan");
            // Faz 4.1 P1/3 fix-2: field-level error key "Input.PublicSlug" olduğunda
            // asp-validation-for="Input.PublicSlug" tag helper field-validation-error
            // class'lı bir span render eder. Bu, key'in doğru bağlandığının kanıtı.
            body.Should().Contain("field-validation-error");

            // Second'in slug'ı atanmamalı
            using var scope2 = _factory.Services.CreateScope();
            var ctx2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx2.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == second.Id);
            profile.PublicSlug.Should().BeNull();
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-first@example.com");
            await RemoveUserIfExistsAsync("pp-second@example.com");
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
                new FormUrlEncodedContent(BaseForm(token, "Preserve Tester",
                    isPublic: false, publicSlug: "preserve-test")));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            // Slug korundu mu?
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
    public async Task OnPost_SlugifiesUserInputWithTurkishCharsAndSpaces()
    {
        var user = await CreateUserAsync("pp-slugify@example.com", "Test User");
        try
        {
            var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            // Kullanıcı dostu input — büyük harf, Türkçe karakter, boşluk
            var response = await client.PostAsync("/profil",
                new FormUrlEncodedContent(BaseForm(token, "Test User",
                    isPublic: true, publicSlug: "İstanbul Hukuk")));
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
                "regex artık server-side slugify ile yer değiştirdi, kullanıcı dostu input kabul edilir");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
            profile.PublicSlug.Should().Be("istanbul-hukuk",
                "SlugHelper Türkçe karakterleri normalize eder, boşluk → tire, lowercase");
        }
        finally
        {
            await RemoveUserIfExistsAsync("pp-slugify@example.com");
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
