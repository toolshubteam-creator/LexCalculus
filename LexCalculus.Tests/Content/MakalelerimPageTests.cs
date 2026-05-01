using System.Net;
using System.Text.RegularExpressions;
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
public class MakalelerimPageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public MakalelerimPageTests(TestAuthWebApplicationFactory factory)
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
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(user, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        return user;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;

        // UserPost cascade ile PostTagLink siler ama post'ları manuel sil
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

    private async Task<int> EnsureCategoryAsync(string slug = "makalelerim-test-kat")
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = await ctx.PostCategories.FirstOrDefaultAsync(x => x.Slug == slug);
        if (c is not null) return c.Id;
        c = new PostCategory
        {
            Name = "Makalelerim Test Kat", Slug = slug, DisplayOrder = 100,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private async Task<int> SeedPostAsync(int userId, int categoryId, string title,
        string slug, bool isPublished)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var post = new UserPost
        {
            UserId = userId, CategoryId = categoryId, Title = title, Slug = slug,
            Body = "<p>içerik</p>", IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            ViewCount = isPublished ? 42 : 0,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(post);
        await ctx.SaveChangesAsync();
        return post.Id;
    }

    [Fact]
    public async Task OnGet_AnonymousUser_RejectsWithChallenge()
    {
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/makalelerim");
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task OnGet_DefaultTab_ReturnsYayinda()
    {
        var u = await SeedUserAsync("mak-default@example.com", "Default User");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Yayinda Post", "mak-yayin-1", isPublished: true);
            await SeedPostAsync(u.Id, catId, "Taslak Post", "mak-taslak-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/makalelerim");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Yayinda Post", "yayında sekme default");
            body.Should().NotContain("Taslak Post", "default tab yayında");
            body.Should().Contain("makalelerim__tab--active");
            body.Should().Contain("42 g", "view count gösterimi (42 görüntülenme)");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnGet_TaslakTab_ReturnsDrafts()
    {
        var u = await SeedUserAsync("mak-taslak@example.com", "Taslak User");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "Yayinda T", "mak-y2", isPublished: true);
            await SeedPostAsync(u.Id, catId, "Taslak T", "mak-t2", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/makalelerim?tab=taslak");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Taslak T");
            body.Should().NotContain("Yayinda T");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnGet_CountsReflectActualData()
    {
        var u = await SeedUserAsync("mak-count@example.com", "Count User");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u.Id, catId, "P1", "mak-c1", isPublished: true);
            await SeedPostAsync(u.Id, catId, "P2", "mak-c2", isPublished: true);
            await SeedPostAsync(u.Id, catId, "D1", "mak-c3", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var body = await (await client.GetAsync("/makalelerim")).Content.ReadAsStringAsync();

            // Sayım badge'i yayında 2, taslak 1 olmalı
            body.Should().MatchRegex(@"Yay.+nda\s*<span[^>]*makalelerim__tab-count[^>]*>\s*2\s*</span>");
            body.Should().MatchRegex(@"Taslak\s*<span[^>]*makalelerim__tab-count[^>]*>\s*1\s*</span>");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnGet_OnlyOwnPosts_OtherUsersHidden()
    {
        var u1 = await SeedUserAsync("mak-own-1@example.com", "Own");
        var u2 = await SeedUserAsync("mak-own-2@example.com", "Other");
        var catId = await EnsureCategoryAsync();
        try
        {
            await SeedPostAsync(u1.Id, catId, "Benim Post", "mak-own-1-p", isPublished: true);
            await SeedPostAsync(u2.Id, catId, "Baskasinin Post", "mak-own-2-p", isPublished: true);

            using var client = CreateAuthClient(u1.Id, u1.Email!, allowAutoRedirect: true);
            var body = await (await client.GetAsync("/makalelerim")).Content.ReadAsStringAsync();

            body.Should().Contain("Benim Post");
            body.Should().NotContain("Baskasinin Post");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }

    [Fact]
    public async Task OnGet_NoPosts_ShowsEmptyMessage()
    {
        var u = await SeedUserAsync("mak-empty@example.com", "Empty");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var body = await (await client.GetAsync("/makalelerim")).Content.ReadAsStringAsync();
            body.Should().Contain("Yay", "boş yayında mesaj");

            var taslakBody = await (await client.GetAsync("/makalelerim?tab=taslak")).Content.ReadAsStringAsync();
            taslakBody.Should().Contain("Taslak makaleniz yok");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostPublish_Draft_SetsPublishedAndRedirects()
    {
        var u = await SeedUserAsync("mak-pub@example.com", "Publish User");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Yayinla Beni", "mak-pub-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim?tab=taslak");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", postId.ToString())
            });
            var response = await client.PostAsync("/makalelerim?handler=Publish", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("yayinda");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.IsPublished.Should().BeTrue();
            refreshed.PublishedAt.Should().NotBeNull();
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostUnpublish_Published_SetsDraftAndRedirects()
    {
        var u = await SeedUserAsync("mak-unpub@example.com", "Unpublish");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Geri Cek", "mak-unpub-1", isPublished: true);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", postId.ToString())
            });
            var response = await client.PostAsync("/makalelerim?handler=Unpublish", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("taslak");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.IsPublished.Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostDelete_Removes_AndRedirectsToReturnTab()
    {
        var u = await SeedUserAsync("mak-del@example.com", "Delete");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Sil Beni", "mak-del-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim?tab=taslak");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", postId.ToString()),
                new KeyValuePair<string, string>("returnTab", "taslak")
            });
            var response = await client.PostAsync("/makalelerim?handler=Delete", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("taslak");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserPosts.AnyAsync(p => p.Id == postId)).Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostDelete_OtherUsersPost_ServiceRejects_PostStillExists()
    {
        var u1 = await SeedUserAsync("mak-attack-1@example.com", "Attacker");
        var u2 = await SeedUserAsync("mak-attack-2@example.com", "Victim");
        var catId = await EnsureCategoryAsync();
        try
        {
            var victimPostId = await SeedPostAsync(u2.Id, catId, "Hedef", "mak-att-vic", isPublished: true);
            // Saldırgan için bir taslak post seed et — sayfa formu render etsin (token için)
            await SeedPostAsync(u1.Id, catId, "Saldırgan Taslak", "mak-att-own", isPublished: false);

            using var client = CreateAuthClient(u1.Id, u1.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim?tab=taslak");
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", victimPostId.ToString())
            });
            var response = await client.PostAsync("/makalelerim?handler=Delete", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
                "PRG yine de redirect döner ama TempData['Error'] olur");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserPosts.AnyAsync(p => p.Id == victimPostId))
                .Should().BeTrue("yetkisiz kullanıcı başkasının postunu silememeli");
        }
        finally
        {
            await CleanupUserAsync(u1.Email!);
            await CleanupUserAsync(u2.Email!);
        }
    }
}
