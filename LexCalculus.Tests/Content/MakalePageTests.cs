using System.Net;
using System.Text.Json;
using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Content;

[Collection("AdminWebHost")]
public class MakalePageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public MakalePageTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    private async Task<(ApplicationUser user, UserProfile profile)> SeedAuthorAsync(
        string email, string fullName, string slug, bool isPublic = true, bool isActive = true)
    {
        await CleanupAsync(email, slug);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            CreatedAt = DateTime.UtcNow, IsActive = isActive, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(u, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        var p = new UserProfile
        {
            UserId = u.Id, DisplayName = fullName, PublicSlug = slug,
            IsPublicProfile = isPublic, Bio = "Test yazar bio"
        };
        ctx.UserProfiles.Add(p);
        await ctx.SaveChangesAsync();
        return (u, p);
    }

    private async Task CleanupAsync(string email, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
        var posts = await ctx.UserPosts.Where(p => p.UserId == u.Id).ToListAsync();
        ctx.UserPosts.RemoveRange(posts);
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    private async Task<int> EnsureCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = await ctx.PostCategories.FirstOrDefaultAsync(x => x.Slug == "is-hukuku");
        if (c is not null) return c.Id;
        c = new PostCategory
        {
            Name = "İş Hukuku", Slug = "is-hukuku",
            DisplayOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private async Task<int> SeedPostAsync(int userId, int catId, string title, string slug,
        bool isPublished, string body = "<p>icerik</p>", string? featuredUrl = null)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = catId, Title = title, Slug = slug,
            Body = body, IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            FeaturedImageUrl = featuredUrl,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task OnGet_InvalidUserSlug_ReturnsNotFound()
    {
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/uye/var-olmayan-yazar/makale/herhangi");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnGet_InvalidPostSlug_ReturnsNotFound()
    {
        var (u, _) = await SeedAuthorAsync("mak-invpost@example.com", "Author", "mak-invp-1");
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-invp-1/makale/var-olmayan");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-invp-1");
        }
    }

    [Fact]
    public async Task OnGet_InactiveAuthor_ReturnsNotFound()
    {
        var (u, _) = await SeedAuthorAsync("mak-inactive@example.com", "Inactive", "mak-ina-1", isActive: false);
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "P", "p1", isPublished: true);
            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-ina-1/makale/p1");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-ina-1");
        }
    }

    [Fact]
    public async Task OnGet_DraftPost_AnonymousViewer_ReturnsNotFound()
    {
        var (u, _) = await SeedAuthorAsync("mak-draft@example.com", "Draft Owner", "mak-dr-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Taslak", "tas-1", isPublished: false);
            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-dr-1/makale/tas-1");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "anonim kullanıcı taslakı görmez");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-dr-1");
        }
    }

    [Fact]
    public async Task OnGet_DraftPost_OwnerViewer_RendersWithPreviewBanner()
    {
        var (u, _) = await SeedAuthorAsync("mak-prev@example.com", "Preview Owner", "mak-pr-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Onizleme", "tas-pr", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!);
            client.DefaultRequestHeaders.Remove("X-Test-User"); // ensure clean
            client.DefaultRequestHeaders.Add("X-Test-User", u.Email!);
            var response = await client.GetAsync("/uye/mak-pr-1/makale/tas-pr");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Onizleme");
            body.Should().Contain("Önizleme", "preview banner görünmeli");
            body.Should().Contain("noindex", "taslak preview robots noindex");
            body.Should().NotContain("application/ld+json",
                "taslak preview JSON-LD üretmez");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-pr-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_AnonymousViewer_RendersAndIncrementsViewCount()
    {
        var (u, _) = await SeedAuthorAsync("mak-pub@example.com", "Pub Author", "mak-pub-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Yayinda", "y-1", isPublished: true);

            using var client = CreateAnonClient();
            var first = await client.GetAsync("/uye/mak-pub-1/makale/y-1");
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            await client.GetAsync("/uye/mak-pub-1/makale/y-1");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.ViewCount.Should().Be(2L, "her anonim GET +1");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-pub-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_OwnerViewer_DoesNotIncrementViewCount()
    {
        var (u, _) = await SeedAuthorAsync("mak-self@example.com", "Self Author", "mak-self-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Self", "self-1", isPublished: true);

            using var client = CreateAuthClient(u.Id, u.Email!);
            await client.GetAsync("/uye/mak-self-1/makale/self-1");
            await client.GetAsync("/uye/mak-self-1/makale/self-1");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.ViewCount.Should().Be(0L, "sahip kendi makalesini sayar mı? hayır");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-self-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_RendersArticleJsonLd()
    {
        var (u, _) = await SeedAuthorAsync("mak-jsonld@example.com", "JsonLd Author", "mak-jl-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "JSON LD Test", "jl-1", isPublished: true,
                body: "<p>Bu makale JSON-LD üretmeli.</p>");

            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-jl-1/makale/jl-1");
            var body = await response.Content.ReadAsStringAsync();

            body.Should().Contain("application/ld+json");
            body.Should().Contain("\"@type\":\"Article\"");
            body.Should().Contain("\"@type\":\"Person\"", "author Person");
            body.Should().Contain("\"@type\":\"Organization\"", "publisher Organization");
            body.Should().Contain("\"articleSection\"");
            body.Should().Contain("JSON LD Test");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-jl-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedWithFeaturedImage_RendersOgImageMeta()
    {
        var (u, _) = await SeedAuthorAsync("mak-og@example.com", "OG Author", "mak-og-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "OG Test", "og-1", isPublished: true,
                featuredUrl: "uploads/posts/1/test.webp");

            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-og-1/makale/og-1");
            var body = await response.Content.ReadAsStringAsync();

            body.Should().Contain("property=\"og:image\"");
            body.Should().Contain("og:type\" content=\"article\"");
            body.Should().Contain("twitter:card\" content=\"summary_large_image\"");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-og-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedWithoutFeaturedImage_FallsBackToSiteDefaultOgImage()
    {
        // SeoMetaProvider site-level default OG image set ediyor (og-default.png).
        // Sayfa explicit FeaturedImage göndermezse, default merge ile gelir —
        // sosyal paylaşımlarda her zaman bir görsel olsun. Featured varsa override eder.
        var (u, _) = await SeedAuthorAsync("mak-noog@example.com", "NoOG", "mak-noog-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Kapaksiz", "nk-1", isPublished: true);

            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-noog-1/makale/nk-1");
            var body = await response.Content.ReadAsStringAsync();

            body.Should().NotContain("uploads/posts/",
                "featured yoksa post-spesifik upload URL meta'da olmamalı");
            body.Should().Contain("og:type\" content=\"article\"",
                "OgType article olmalı (article tipi korunur)");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-noog-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_RendersCommentSection()
    {
        var (u, _) = await SeedAuthorAsync("mak-cs@example.com", "CS Author", "mak-cs-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "CS Test", "cs-1", isPublished: true);

            using var client = CreateAnonClient();
            var body = await (await client.GetAsync("/uye/mak-cs-1/makale/cs-1")).Content.ReadAsStringAsync();
            body.Should().Contain("yorumlar", "Yorumlar bölümü id");
            body.Should().Contain("yorumlar__title");
            body.Should().Contain("Henüz yorum yok");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-cs-1");
        }
    }

    [Fact]
    public async Task OnGet_AnonymousViewer_ShowsLoginLinkInsteadOfCommentForm()
    {
        var (u, _) = await SeedAuthorAsync("mak-anon@example.com", "Anon Author", "mak-anon-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Anon Test", "anon-1", isPublished: true);

            using var client = CreateAnonClient();
            var body = await (await client.GetAsync("/uye/mak-anon-1/makale/anon-1")).Content.ReadAsStringAsync();

            body.Should().Contain("yorum-form__login");
            body.Should().NotContain("id=\"yorum-form\"", "anonim form göstermez");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-anon-1");
        }
    }

    [Fact]
    public async Task OnGet_AuthenticatedViewer_ShowsCommentForm()
    {
        var (author, _) = await SeedAuthorAsync("mak-auth@example.com", "Auth Author", "mak-auth-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(author.Id, catId, "Auth Test", "auth-1", isPublished: true);

            using var client = CreateAuthClient(author.Id, author.Email!);
            var body = await (await client.GetAsync("/uye/mak-auth-1/makale/auth-1")).Content.ReadAsStringAsync();

            body.Should().Contain("id=\"yorum-form\"");
            body.Should().Contain("Yorumu Gönder");
            body.Should().Contain("makale__like", "beğeni butonu authenticated");
        }
        finally
        {
            await CleanupAsync(author.Email!, "mak-auth-1");
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_RendersDisclaimerAndAuthorFooter()
    {
        var (u, _) = await SeedAuthorAsync("mak-disc@example.com", "Disclaimer Author", "mak-d-1");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Disc Test", "d-1", isPublished: true);

            using var client = CreateAnonClient();
            var body = await (await client.GetAsync("/uye/mak-d-1/makale/d-1")).Content.ReadAsStringAsync();

            body.Should().Contain("makale__disclaimer", "disclaimer kutusu render");
            body.Should().Contain("Hukuki tavsiye",
                "disclaimer kelime kontrolü (Karar 8)");
            body.Should().Contain("makale__author-footer", "yazar bloğu");
            body.Should().Contain("Test yazar bio");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-d-1");
        }
    }

    // ─── Faz 5.3 Hide moderation ──────────────────────────────────────────

    private async Task SetPostHiddenAsync(int postId, bool hidden)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var post = await ctx.UserPosts.FirstAsync(p => p.Id == postId);
        post.IsModeratorHidden = hidden;
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task OnGet_HiddenPost_AnonymousReturns404()
    {
        var (u, _) = await SeedAuthorAsync("hide-anon@example.com", "Hide Anon", "mak-hidden-anon");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "H", "h-1", isPublished: true);
            await SetPostHiddenAsync(postId, true);

            using var client = CreateAnonClient();
            var response = await client.GetAsync("/uye/mak-hidden-anon/makale/h-1");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-hidden-anon");
        }
    }

    [Fact]
    public async Task OnGet_HiddenPost_OwnerSeesHiddenBanner()
    {
        var (u, _) = await SeedAuthorAsync("hide-own@example.com", "Hide Own", "mak-hidden-own");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "H", "h-2", isPublished: true);
            await SetPostHiddenAsync(postId, true);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var response = await client.GetAsync("/uye/mak-hidden-own/makale/h-2");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("makale__preview-banner--hidden",
                "sahip için 'Yönetim tarafından gizlendi' banner");
            body.Should().Contain("noindex", "hidden preview noindex meta render");

            // Faz 5.3 fix — hidden preview'da yorum + beğeni interaction gizli
            body.Should().NotContain("class=\"yorumlar\"",
                "yorum section hidden preview'da render edilmemeli");
            body.Should().NotContain("data-post-id=\"" + postId + "\"",
                "beğeni butonu hidden preview'da render edilmemeli");
        }
        finally
        {
            await CleanupAsync(u.Email!, "mak-hidden-own");
        }
    }
}
