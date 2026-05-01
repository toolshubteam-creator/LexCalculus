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
public class MakaleYeniPageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public MakaleYeniPageTests(TestAuthWebApplicationFactory factory)
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
        // PostCategorySeeder zaten boot'ta çalışır, fallback
        c = new PostCategory
        {
            Name = "İş Hukuku", Slug = "is-hukuku", DisplayOrder = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        ctx.PostCategories.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task OnGet_RendersFormWithCategories()
    {
        var u = await SeedUserAsync("makyeni-get@example.com", "Form User");
        await EnsureCategoryAsync();
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var response = await client.GetAsync("/makalelerim/yeni");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Yeni Makale");
            body.Should().Contain("quill-editor");
            body.Should().Contain("tag-chip-container");
            body.Should().Contain("Hukuku", "kategori dropdown'da görünmeli (Türkçe encoding)");
            body.Should().Contain("__RequestVerificationToken");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostSaveDraft_Valid_RedirectsToTaslakAndPersists()
    {
        var u = await SeedUserAsync("makyeni-draft@example.com", "Draft User");
        var catId = await EnsureCategoryAsync();
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim/yeni");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "Yeni Taslak"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>içerik</p>"),
                new KeyValuePair<string, string>("Input.TagsCsv", "kvkk, iş hukuku")
            });
            var response = await client.PostAsync("/makalelerim/yeni?handler=SaveDraft", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("taslak");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var post = await ctx.UserPosts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == u.Id && p.Title == "Yeni Taslak");
            post.Should().NotBeNull();
            post!.IsPublished.Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostPublish_Valid_RedirectsToYayindaAndPublishes()
    {
        var u = await SeedUserAsync("makyeni-pub@example.com", "Pub User");
        var catId = await EnsureCategoryAsync();
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim/yeni");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "Yayinda Test"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>içerik</p>"),
                new KeyValuePair<string, string>("Input.TagsCsv", "")
            });
            var response = await client.PostAsync("/makalelerim/yeni?handler=Publish", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("yayinda");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var post = await ctx.UserPosts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == u.Id && p.Title == "Yayinda Test");
            post.Should().NotBeNull();
            post!.IsPublished.Should().BeTrue();
            post.PublishedAt.Should().NotBeNull();
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostSaveDraft_MissingTitle_ReturnsViewWithError()
    {
        var u = await SeedUserAsync("makyeni-empty@example.com", "Empty Title");
        var catId = await EnsureCategoryAsync();
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!, allowAutoRedirect: true);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim/yeni");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", ""),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>içerik</p>")
            });
            var response = await client.PostAsync("/makalelerim/yeni?handler=SaveDraft", form);

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "validation hatası → form yeniden render");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("zorunlu", "Title boş hatası");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }

    [Fact]
    public async Task OnPostSaveDraft_BodyWithScript_StripsBeforePersist()
    {
        var u = await SeedUserAsync("makyeni-xss@example.com", "XSS Tester");
        var catId = await EnsureCategoryAsync();
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var token = await GetAntiforgeryTokenAsync(client, "/makalelerim/yeni");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Input.Title", "XSS Test"),
                new KeyValuePair<string, string>("Input.CategoryId", catId.ToString()),
                new KeyValuePair<string, string>("Input.Body", "<p>safe</p><script>alert(1)</script>")
            });
            await client.PostAsync("/makalelerim/yeni?handler=SaveDraft", form);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var post = await ctx.UserPosts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == u.Id && p.Title == "XSS Test");
            post.Should().NotBeNull();
            post!.Body.Should().NotContain("<script", "DB'de script tag olmamalı");
            post.Body.Should().Contain("safe");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }
}
