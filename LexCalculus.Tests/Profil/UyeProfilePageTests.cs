using System.Net;
using System.Text.Json;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Profil;

[Collection("AdminWebHost")]
public class UyeProfilePageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public UyeProfilePageTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private async Task<(ApplicationUser user, UserProfile profile)> SeedAsync(
        string email,
        string displayName,
        string slug,
        bool isPublic,
        bool isActive = true,
        bool showTenant = false,
        int? tenantId = null,
        string? bio = null,
        string? city = null,
        MeslekTuru? meslek = null,
        string? meslekDiger = null,
        string? avatarUrl = null,
        string? baroNo = null)
    {
        await CleanupAsync(email, slug);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsActive = isActive,
            EmailConfirmed = true,
            TenantId = tenantId,
            PhoneNumber = "5551234567"
        };
        var r = await um.CreateAsync(user, "ValidPass123!");
        r.Succeeded.Should().BeTrue();

        var profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            PublicSlug = slug,
            IsPublicProfile = isPublic,
            ShowTenant = showTenant,
            Bio = bio,
            City = city,
            MeslekTuru = meslek,
            MeslekTuruDiger = meslekDiger,
            AvatarUrl = avatarUrl,
            BaroNo = baroNo
        };
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();

        return (user, profile);
    }

    private async Task CleanupAsync(string email, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var byEmail = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        var bySlug = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.PublicSlug == slug);
        if (bySlug is not null) ctx.UserProfiles.Remove(bySlug);
        if (byEmail is not null)
        {
            var profile = await ctx.UserProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == byEmail.Id);
            if (profile is not null) ctx.UserProfiles.Remove(profile);
            ctx.Users.Remove(byEmail);
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task OnGet_ReturnsNotFoundWhenSlugMissing()
    {
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/uye/var-olmayan-slug-xyz-123");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnGet_ReturnsNotFoundWhenUserInactive()
    {
        var slug = "uye-inactive-test";
        var (user, _) = await SeedAsync("uye-inactive@example.com", "Inactive User", slug,
            isPublic: true, isActive: false);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PublicProfile_ShowsAllPublicFieldsInHtml()
    {
        var slug = "uye-public-test";
        var (user, _) = await SeedAsync("uye-public@example.com", "Mesut Avukat", slug,
            isPublic: true, bio: "Hukuk alanında 10 yıl tecrübe.", city: "İstanbul",
            meslek: MeslekTuru.Avukat);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Mesut Avukat");
            body.Should().Contain("Avukat");
            // Türkçe encoding korunsun (Razor HtmlEncoder İ → &#x130;)
            (body.Contains("stanbul") || body.Contains("&#x130;stanbul"))
                .Should().BeTrue("İstanbul ya raw substring ya da HtmlEncoder ile encode edilmiş olmalı");
            body.Should().Contain("Hukuk alan");

            // JSON-LD bloğu
            body.Should().Contain("application/ld+json");
            body.Should().Contain("\"@type\":\"Person\"");
            body.Should().Contain("\"jobTitle\":\"Avukat\"");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PrivateProfile_ShowsOnlyDefaults()
    {
        var slug = "uye-private-test";
        var (user, _) = await SeedAsync("uye-private@example.com", "Gizli Avukat", slug,
            isPublic: false, bio: "Bio var ama gösterilmemeli", city: "Ankara",
            meslek: MeslekTuru.Hakim);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Gizli Avukat");
            body.Should().Contain("gizli tutuluyor");
            body.Should().NotContain("Bio var ama");
            body.Should().NotContain("Ankara");
            body.Should().NotContain("Hâkim");

            // JSON-LD'de minimal — sadece name + url; jobTitle/description YOK.
            // (Layout `<meta name="description">` HTML genelinde her zaman var, bu
            // yüzden tüm body üzerinde NotContain kullanılamaz; JSON-LD bloğunu
            // izole edip parse et.)
            body.Should().Contain("\"@context\":\"https://schema.org\"");
            var ldStart = body.IndexOf("\"@context\":\"https://schema.org\"", StringComparison.Ordinal);
            var ldEndScript = body.IndexOf("</script>", ldStart, StringComparison.Ordinal);
            var ldRaw = body.Substring(ldStart - 1, ldEndScript - (ldStart - 1));
            var openBrace = ldRaw.IndexOf('{');
            var closeBrace = ldRaw.LastIndexOf('}');
            var json = ldRaw.Substring(openBrace, closeBrace - openBrace + 1);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("@type").GetString().Should().Be("Person");
            root.TryGetProperty("jobTitle", out _).Should().BeFalse(
                "private profile JSON-LD'de jobTitle olmamalı");
            root.TryGetProperty("description", out _).Should().BeFalse(
                "private profile JSON-LD'de description olmamalı");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_ShowTenantFalse_HidesTenantEvenWhenPublic()
    {
        // Sahte tenant seed
        int tenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new ApplicationUser
            {
                UserName = "owner-uye-test@x.com", Email = "owner-uye-test@x.com",
                FullName = "Tenant Sahibi", CreatedAt = DateTime.UtcNow,
                IsActive = true, EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString()
            };
            ctx.Users.Add(owner);
            await ctx.SaveChangesAsync();
            var t = new Tenant
            {
                Name = "Aslan Hukuk Bürosu", Slug = "aslan-hukuk-uye-test",
                OwnerUserId = owner.Id, CreatedAt = DateTime.UtcNow
            };
            ctx.Tenants.Add(t);
            await ctx.SaveChangesAsync();
            tenantId = t.Id;
        }

        var slug = "uye-show-tenant-false";
        var (user, _) = await SeedAsync("uye-st@example.com", "Tenant Üyesi", slug,
            isPublic: true, showTenant: false, tenantId: tenantId);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("Aslan Hukuk Bürosu");
            body.Should().NotContain("Hukuk Bürosu:");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task GetPersonJsonLd_NeverIncludesBaroNoOrEmailOrPhone()
    {
        // KVKK regression koruması — test sayfayı çek, JSON-LD'yi parse et,
        // hassas alanların hiçbir koşulda dahil edilmediğini doğrula.
        var slug = "uye-kvkk-test";
        var (user, _) = await SeedAsync(
            email: "kvkk-leak@example.com",
            displayName: "KVKK Test Avukatı",
            slug: slug,
            isPublic: true,
            bio: "Test bio with hassas info",
            city: "İstanbul",
            meslek: MeslekTuru.Avukat,
            baroNo: "ISTANBUL-12345");
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();

            // Sayfada ham haldeki hassas alanlar GÖRÜNMEZ
            body.Should().NotContain("ISTANBUL-12345", "BaroNo profile sayfasında render edilmez");
            body.Should().NotContain("kvkk-leak@example.com", "e-posta profile sayfasında render edilmez");
            body.Should().NotContain("5551234567", "telefon profile sayfasında render edilmez");

            // JSON-LD'de de yok — keys check
            var jsonLdStart = body.IndexOf("\"@context\":\"https://schema.org\"", StringComparison.Ordinal);
            jsonLdStart.Should().BeGreaterThan(-1);
            var jsonLdEnd = body.IndexOf("</script>", jsonLdStart, StringComparison.Ordinal);
            var jsonLdRaw = body.Substring(jsonLdStart - 1, jsonLdEnd - (jsonLdStart - 1));
            // JSON parsing — başında { olduğunu varsay
            var openBrace = jsonLdRaw.IndexOf('{');
            var closeBrace = jsonLdRaw.LastIndexOf('}');
            var json = jsonLdRaw.Substring(openBrace, closeBrace - openBrace + 1);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Yasak key'lerin tümü yokluğu
            root.TryGetProperty("baroNo", out _).Should().BeFalse();
            root.TryGetProperty("BaroNo", out _).Should().BeFalse();
            root.TryGetProperty("email", out _).Should().BeFalse();
            root.TryGetProperty("Email", out _).Should().BeFalse();
            root.TryGetProperty("telephone", out _).Should().BeFalse();
            root.TryGetProperty("phone", out _).Should().BeFalse();
            root.TryGetProperty("phoneNumber", out _).Should().BeFalse();
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }
}
