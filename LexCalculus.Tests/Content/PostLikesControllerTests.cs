using System.Net;
using System.Net.Http.Json;
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
public class PostLikesControllerTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public PostLikesControllerTests(SqlServerTestAuthWebApplicationFactory factory)
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

    private async Task<ApplicationUser> SeedUserAsync(string email)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, FullName = "Liker",
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
        var likes = await ctx.PostLikes.Where(l => l.UserId == u.Id).ToListAsync();
        ctx.PostLikes.RemoveRange(likes);
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

    private async Task<int> SeedPostAsync(int userId, int catId, string slug, bool isPublished)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = catId, Title = "T", Slug = slug,
            Body = "<p>x</p>", IsPublished = isPublished,
            PublishedAt = isPublished ? now : null,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<string> GetCsrfAsync(HttpClient client, string url = "/profil")
    {
        var html = await client.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!m.Success) throw new InvalidOperationException($"Token not found at {url}");
        return m.Groups[1].Value;
    }

    [Fact]
    public async Task Toggle_Anonymous_ReturnsUnauthorizedOrBadRequest()
    {
        using var client = CreateAnonClient();
        var response = await client.PostAsJsonAsync("/api/post-likes/toggle", new { postId = 1 });
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Toggle_FirstTime_AddsLikeAndReturnsCount()
    {
        var owner = await SeedUserAsync("plc-fto@example.com");
        var liker = await SeedUserAsync("plc-ftl@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "plc-ft", isPublished: true);

            using var client = CreateAuthClient(liker.Id, liker.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var response = await client.PostAsJsonAsync("/api/post-likes/toggle",
                new { postId });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = await response.Content.ReadFromJsonAsync<ToggleResp>();
            data!.isLiked.Should().BeTrue();
            data.likeCount.Should().Be(1);
        }
        finally
        {
            await CleanupUserAsync(liker.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Toggle_SecondTime_RemovesLike()
    {
        var owner = await SeedUserAsync("plc-sto@example.com");
        var liker = await SeedUserAsync("plc-stl@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "plc-st", isPublished: true);

            using var client = CreateAuthClient(liker.Id, liker.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            await client.PostAsJsonAsync("/api/post-likes/toggle", new { postId });
            var second = await client.PostAsJsonAsync("/api/post-likes/toggle", new { postId });

            second.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = await second.Content.ReadFromJsonAsync<ToggleResp>();
            data!.isLiked.Should().BeFalse();
            data.likeCount.Should().Be(0);
        }
        finally
        {
            await CleanupUserAsync(liker.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Toggle_DraftPost_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("plc-dro@example.com");
        var liker = await SeedUserAsync("plc-drl@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "plc-dr", isPublished: false);

            using var client = CreateAuthClient(liker.Id, liker.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var response = await client.PostAsJsonAsync("/api/post-likes/toggle", new { postId });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(liker.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    private sealed class ToggleResp
    {
        public bool isLiked { get; set; }
        public int likeCount { get; set; }
    }
}
