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
public class MakaleDuzenlePageTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public MakaleDuzenlePageTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId, string email, bool allowAutoRedirect = false)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
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
        var u = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(u, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        return u;
    }

    private async Task CleanupUserAsync(string email)
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
            Name = "İş Hukuku", Slug = "is-hukuku", DisplayOrder = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private async Task<int> SeedPostAsync(int userId, int catId, string title, string slug, bool isPublished)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = catId, Title = title, Slug = slug,
            Body = "<p>orijinal</p>", IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task OnGet_NonOwner_ReturnsNotFound()
    {
        var owner = await SeedUserAsync("makduz-owner@example.com", "Owner");
        var attacker = await SeedUserAsync("makduz-attack@example.com", "Attacker");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "Owner Post", "makduz-o-1", isPublished: true);

            using var client = CreateAuthClient(attacker.Id, attacker.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync($"/makalelerim/duzenle/{postId}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupUserAsync(attacker.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task OnGet_Owner_LoadsExistingValues()
    {
        var u = await SeedUserAsync("makduz-load@example.com", "Loader");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Yuklenecek", "makduz-load-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync($"/makalelerim/duzenle/{postId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Yuklenecek", "mevcut başlık form'da");
            body.Should().Contain("Taslak Kaydet", "taslak için Taslak Kaydet butonu");
            body.Should().Contain("Yay", "yayinla butonu da var (taslak için)");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnGet_PublishedPost_ShowsSaveAndUnpublishButtons()
    {
        var u = await SeedUserAsync("makduz-pubload@example.com", "PubLoader");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "PubPost", "makduz-pl-1", isPublished: true);

            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync($"/makalelerim/duzenle/{postId}");

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Kaydet", "yayında postta tek 'Kaydet' butonu");
            body.Should().Contain("Tasla", "ya da 'Taslağa Al' butonu");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostSave_DraftPost_KeepsDraftAndUpdates()
    {
        var u = await SeedUserAsync("makduz-save@example.com", "Saver");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Eski Baslik", "makduz-s-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, $"/makalelerim/duzenle/{postId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "Yeni Baslik"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>guncel</p>"),
                new KeyValuePair<string, string>("Input.TagsCsv", "")
            });
            var response = await client.PostAsync(
                $"/makalelerim/duzenle/{postId}?handler=SaveDraft", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.Title.Should().Be("Yeni Baslik");
            refreshed.IsPublished.Should().BeFalse("draft kalsın");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostPublish_FromDraft_PublishesAndRedirects()
    {
        var u = await SeedUserAsync("makduz-pub@example.com", "Pub");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Yayinla", "makduz-p-1", isPublished: false);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, $"/makalelerim/duzenle/{postId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "Yayinla"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>icerik</p>"),
                new KeyValuePair<string, string>("Input.TagsCsv", "")
            });
            var response = await client.PostAsync(
                $"/makalelerim/duzenle/{postId}?handler=Publish", form);

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
    public async Task OnPostSaveDraft_FromPublished_Unpublishes()
    {
        var u = await SeedUserAsync("makduz-unpub@example.com", "Unpub");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(u.Id, catId, "Geri Cek", "makduz-u-1", isPublished: true);

            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, $"/makalelerim/duzenle/{postId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "Geri Cek"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>guncel</p>"),
                new KeyValuePair<string, string>("Input.TagsCsv", "")
            });
            var response = await client.PostAsync(
                $"/makalelerim/duzenle/{postId}?handler=SaveDraft", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("taslak");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.UserPosts.AsNoTracking().FirstAsync(p => p.Id == postId);
            refreshed.IsPublished.Should().BeFalse("Unpublish edildi");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }
}
