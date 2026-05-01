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
        bool showConnections = false,
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
            ShowConnections = showConnections,
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
            var conns = await ctx.UserConnections
                .Where(c => c.RequesterId == byEmail.Id || c.TargetId == byEmail.Id)
                .ToListAsync();
            ctx.UserConnections.RemoveRange(conns);

            var blocks = await ctx.UserBlocks
                .Where(b => b.BlockerId == byEmail.Id || b.BlockedId == byEmail.Id)
                .ToListAsync();
            ctx.UserBlocks.RemoveRange(blocks);

            var profile = await ctx.UserProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == byEmail.Id);
            if (profile is not null) ctx.UserProfiles.Remove(profile);
            ctx.Users.Remove(byEmail);
        }
        await ctx.SaveChangesAsync();
    }

    private HttpClient CreateAuthClient(int userId, string email, bool allowAutoRedirect = true)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<int> SeedConnectionAsync(int requesterId, int targetId,
        LexCalculus.Core.Entities.Social.UserConnectionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new LexCalculus.Core.Entities.Social.UserConnection
        {
            RequesterId = requesterId,
            TargetId = targetId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            RespondedAt = status != LexCalculus.Core.Entities.Social.UserConnectionStatus.Pending
                ? DateTime.UtcNow : null
        };
        ctx.UserConnections.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
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

    // ───────── Faz 4.2 P3a/3 — bağlantı butonu state-aware testler ─────────

    [Fact]
    public async Task OnGet_AnonymousViewer_RendersLoginCta()
    {
        var slug = "uye-anon-cta";
        var (user, _) = await SeedAsync("uye-anon-cta@example.com", "Anon CTA Test", slug,
            isPublic: true);
        try
        {
            using var client = CreateAnonClient();
            client.DefaultRequestHeaders.Remove("X-Test-User");
            // AllowAutoRedirect default false → 200 OK with anonymous body
            var response = await _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            }).GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("giriş yap", "anonim viewer için CTA gösterilir");
            body.Should().Contain("ReturnUrl");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_OwnProfile_RendersEditLink()
    {
        var slug = "uye-own-edit";
        var (user, _) = await SeedAsync("uye-own@example.com", "Own Profile Test", slug,
            isPublic: true);
        try
        {
            using var client = CreateAuthClient(user.Id, user.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Profilinizi D", "kendi profilim için edit link");
            body.Should().NotContain("Bağlantı İste");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_OtherUser_NoneState_RendersConnectButton()
    {
        var viewer = await CreateUserAsync("uye-c-viewer@example.com", "Viewer");
        var slug = "uye-c-target";
        var (target, _) = await SeedAsync("uye-c-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Bağlantı İste");
            body.Should().Contain("handler=Connect", "POST handler form attr");
        }
        finally
        {
            await CleanupAsync(viewer.Email!, $"slug-{viewer.Id}");
            // viewer'ın slug'ı bilinmediği için hem mail hem (best effort) slug temizliği
            using (var scope = _factory.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var stale = await ctx.Users.FirstOrDefaultAsync(u => u.Email == viewer.Email);
                if (stale is not null)
                {
                    var p = await ctx.UserProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.UserId == stale.Id);
                    if (p is not null) ctx.UserProfiles.Remove(p);
                    ctx.Users.Remove(stale);
                    await ctx.SaveChangesAsync();
                }
            }
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_OtherUser_PendingSent_RendersCancelWithConnectionId()
    {
        var viewer = await CreateUserAsync("uye-ps-viewer@example.com", "Viewer");
        var slug = "uye-ps-target";
        var (target, _) = await SeedAsync("uye-ps-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            var connId = await SeedConnectionAsync(viewer.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Pending);

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("İstek g", "PendingSent status mesajı");
            body.Should().Contain("İptal Et");
            body.Should().Contain($"value=\"{connId}\"", "ConnectionId hidden input'ta");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_OtherUser_Accepted_ShowsConnectionCount()
    {
        var viewer = await CreateUserAsync("uye-ac-viewer@example.com", "Viewer");
        var slug = "uye-ac-target";
        var (target, _) = await SeedAsync("uye-ac-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            await SeedConnectionAsync(viewer.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Bağl", "Accepted state mesajı");
            body.Should().Contain("Bağlantıyı Kald", "Remove butonu");
            body.Should().Contain("uye-profil__connection-count", "sayı bloğu render");
            body.Should().Contain(">1<", "1 bağlantı sayısı");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PrivateProfile_DoesNotRenderConnectionCount()
    {
        var slug = "uye-private-no-count";
        var (target, _) = await SeedAsync("uye-pc@example.com", "Private Count", slug,
            isPublic: false);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("uye-profil__connection-count",
                "private profile'da bağlantı sayısı gösterilmez (Karar 5)");
        }
        finally
        {
            await CleanupAsync(target.Email!, slug);
        }
    }

    // Faz 4.2 P3b/3 — count tıklanabilir koşulu
    [Fact]
    public async Task OnGet_ShowConnectionsTrue_RendersCountAsLink()
    {
        var viewer = await CreateUserAsync("uye-cl-viewer@example.com", "Viewer");
        var slug = "uye-cl-target";
        var (target, _) = await SeedAsync("uye-cl-target@example.com", "Target", slug,
            isPublic: true, showConnections: true);
        try
        {
            await SeedConnectionAsync(viewer.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("uye-profil__connection-count--link",
                "ShowConnections=true → link variant render");
            body.Should().Contain($"href=\"/uye/{slug}/baglantilar\"");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_ShowConnectionsFalse_RendersCountAsStaticDiv()
    {
        var viewer = await CreateUserAsync("uye-cs-viewer@example.com", "Viewer");
        var slug = "uye-cs-target";
        var (target, _) = await SeedAsync("uye-cs-target@example.com", "Target", slug,
            isPublic: true, showConnections: false);
        try
        {
            await SeedConnectionAsync(viewer.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("uye-profil__connection-count");
            body.Should().NotContain("uye-profil__connection-count--link",
                "ShowConnections=false → statik div, link değil");
            body.Should().NotContain($"href=\"/uye/{slug}/baglantilar\"");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    // Faz 4.3 — engelleme entegrasyonu

    [Fact]
    public async Task OnGet_BlockedByMe_RendersUnblockButton()
    {
        var viewer = await CreateUserAsync("uye-bm-viewer@example.com", "Viewer");
        var slug = "uye-bm-target";
        var (target, _) = await SeedAsync("uye-bm-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var blockSvc = new LexCalculus.Infrastructure.Services.UserBlockService(
                    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                    new LexCalculus.Tests.TestHelpers.NullActivityLogService());
                await blockSvc.BlockAsync(viewer.Id, target.Id);
            }

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Bu kullan", "BlockedByMe mesajı");
            body.Should().Contain("Engellemeyi Kald", "Unblock butonu");
            body.Should().NotContain("Bağlantı İste", "Connect butonu engellemede gizli");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_BlockedByOther_RendersSilentInteraction()
    {
        var viewer = await CreateUserAsync("uye-bo-viewer@example.com", "Viewer");
        var slug = "uye-bo-target";
        var (target, _) = await SeedAsync("uye-bo-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            // target → viewer engelliyor; viewer profili ziyaret ediyor
            using (var scope = _factory.Services.CreateScope())
            {
                var blockSvc = new LexCalculus.Infrastructure.Services.UserBlockService(
                    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                    new LexCalculus.Tests.TestHelpers.NullActivityLogService());
                await blockSvc.BlockAsync(target.Id, viewer.Id);
            }

            using var client = CreateAuthClient(viewer.Id, viewer.Email!);
            var response = await client.GetAsync($"/uye/{slug}");
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "engelleme görünürlüğü etkilemez, sayfa açılır");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("Bağlantı İste",
                "BlockedByOther: Connect butonu YOK (sessiz pattern)");
            body.Should().NotContain("Engelle",
                "BlockedByOther: Engelle butonu YOK");
            body.Should().NotContain("engelled",
                "BlockedByOther: 'engellediniz' mesajı da YOK (sessiz)");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnPostBlock_Valid_CreatesBlockAndRemovesAcceptedConnection()
    {
        var viewer = await CreateUserAsync("uye-pb-viewer@example.com", "Viewer");
        var slug = "uye-pb-target";
        var (target, _) = await SeedAsync("uye-pb-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            await SeedConnectionAsync(viewer.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Accepted);

            using var client = CreateAuthClient(viewer.Id, viewer.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, $"/uye/{slug}");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });
            var response = await client.PostAsync($"/uye/{slug}?handler=Block", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserBlocks.AnyAsync(b => b.BlockerId == viewer.Id && b.BlockedId == target.Id))
                .Should().BeTrue();
            (await ctx.UserConnections.AnyAsync(c =>
                (c.RequesterId == viewer.Id && c.TargetId == target.Id)
                || (c.RequesterId == target.Id && c.TargetId == viewer.Id)))
                .Should().BeFalse("cascade ile bağlantı silindi");
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnPostConnect_ValidTarget_CreatesPendingAndRedirects()
    {
        var viewer = await CreateUserAsync("uye-pc-viewer@example.com", "Viewer");
        var slug = "uye-pc-target";
        var (target, _) = await SeedAsync("uye-pc-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            using var client = CreateAuthClient(viewer.Id, viewer.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, $"/uye/{slug}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token)
            });
            var response = await client.PostAsync($"/uye/{slug}?handler=Connect", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conn = await ctx.UserConnections.AsNoTracking()
                .FirstOrDefaultAsync(c => c.RequesterId == viewer.Id && c.TargetId == target.Id);
            conn.Should().NotBeNull();
            conn!.Status.Should().Be(LexCalculus.Core.Entities.Social.UserConnectionStatus.Pending);
        }
        finally
        {
            await CleanupRawUserAsync(viewer.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    [Fact]
    public async Task OnPostAccept_PendingReceived_SetsAcceptedInDb()
    {
        var sender = await CreateUserAsync("uye-ap-sender@example.com", "Sender");
        var slug = "uye-ap-target";
        var (target, _) = await SeedAsync("uye-ap-target@example.com", "Target", slug,
            isPublic: true);
        try
        {
            var connId = await SeedConnectionAsync(sender.Id, target.Id,
                LexCalculus.Core.Entities.Social.UserConnectionStatus.Pending);

            using var client = CreateAuthClient(target.Id, target.Email!, allowAutoRedirect: false);
            var token = await GetAntiforgeryTokenAsync(client, "/profil");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("connectionId", connId.ToString())
            });
            var response = await client.PostAsync($"/uye/{slug}?handler=Accept", form);
            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conn = await ctx.UserConnections.AsNoTracking().FirstAsync(c => c.Id == connId);
            conn.Status.Should().Be(LexCalculus.Core.Entities.Social.UserConnectionStatus.Accepted);
        }
        finally
        {
            await CleanupRawUserAsync(sender.Email!);
            await CleanupAsync(target.Email!, slug);
        }
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task<ApplicationUser> CreateUserAsync(string email, string fullName)
    {
        await CleanupRawUserAsync(email);
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
        // Profile lazy create — /profil OnGet bunu yapacak; testte direkt ekleyelim
        // (PublicSlug null kalsın ki sadece /baglantilarim'a girince oluşacağı tipik
        //  davranışı simüle edelim).
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id, DisplayName = fullName,
            PublicSlug = $"viewer-{Guid.NewGuid():N}".Substring(0, 20)
        });
        await ctx.SaveChangesAsync();
        return user;
    }

    private async Task CleanupRawUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
        var conns = await ctx.UserConnections
            .Where(c => c.RequesterId == u.Id || c.TargetId == u.Id).ToListAsync();
        ctx.UserConnections.RemoveRange(conns);
        var blocks = await ctx.UserBlocks
            .Where(b => b.BlockerId == u.Id || b.BlockedId == u.Id).ToListAsync();
        ctx.UserBlocks.RemoveRange(blocks);
        var p = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (p is not null) ctx.UserProfiles.Remove(p);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = System.Text.RegularExpressions.Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }
}
