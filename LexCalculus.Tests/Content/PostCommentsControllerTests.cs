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
public class PostCommentsControllerTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public PostCommentsControllerTests(SqlServerTestAuthWebApplicationFactory factory)
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
            UserName = email, Email = email, FullName = "Tester",
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
        var comments = await ctx.PostComments.Where(c => c.UserId == u.Id).ToListAsync();
        ctx.PostComments.RemoveRange(comments);
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
    public async Task Create_Anonymous_ReturnsUnauthorizedOrBadRequest()
    {
        using var client = CreateAnonClient();
        var response = await client.PostAsJsonAsync("/api/post-comments/create",
            new { postId = 1, body = "x" });
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Valid_ReturnsOkWithHtml()
    {
        var owner = await SeedUserAsync("pcc-owner@example.com");
        var commenter = await SeedUserAsync("pcc-commenter@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-create", isPublished: true);

            using var client = CreateAuthClient(commenter.Id, commenter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var response = await client.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "Bu bir test yorumudur." });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = await response.Content.ReadFromJsonAsync<CreateResp>();
            data.Should().NotBeNull();
            data!.html.Should().Contain("yorum");
            data.html.Should().Contain("Bu bir test yorumudur");
            data.commentId.Should().BeGreaterThan(0);
        }
        finally
        {
            await CleanupUserAsync(commenter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Create_DraftPost_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("pcc-draft-o@example.com");
        var commenter = await SeedUserAsync("pcc-draft-c@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-draft", isPublished: false);

            using var client = CreateAuthClient(commenter.Id, commenter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var response = await client.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "yorum" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(commenter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Update_NonOwner_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("pcc-upo@example.com");
        var c1 = await SeedUserAsync("pcc-up1@example.com");
        var c2 = await SeedUserAsync("pcc-up2@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-up", isPublished: true);

            // c1 yorum yapar, c2 düzenlemeye çalışır
            using var clientC1 = CreateAuthClient(c1.Id, c1.Email!);
            var token1 = await GetCsrfAsync(clientC1);
            clientC1.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token1);
            var createResp = await clientC1.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "ilk yorum" });
            createResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var created = await createResp.Content.ReadFromJsonAsync<CreateResp>();

            using var clientC2 = CreateAuthClient(c2.Id, c2.Email!);
            var token2 = await GetCsrfAsync(clientC2);
            clientC2.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token2);
            var updateResp = await clientC2.PostAsJsonAsync(
                $"/api/post-comments/{created!.commentId}/update",
                new { body = "kötü değişim" });

            updateResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(c1.Email!);
            await CleanupUserAsync(c2.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Update_Valid_ReturnsOkWithUpdatedHtml()
    {
        var owner = await SeedUserAsync("pcc-uvo@example.com");
        var commenter = await SeedUserAsync("pcc-uvc@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-uv", isPublished: true);

            using var client = CreateAuthClient(commenter.Id, commenter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var createResp = await client.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "ilk" });
            var created = await createResp.Content.ReadFromJsonAsync<CreateResp>();

            var updateResp = await client.PostAsJsonAsync(
                $"/api/post-comments/{created!.commentId}/update",
                new { body = "düzeltildi" });

            updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var data = await updateResp.Content.ReadFromJsonAsync<UpdateResp>();
            data!.html.Should().Contain("düzeltildi");
            data.html.Should().Contain("düzenlendi", "IsEdited badge görünmeli");
        }
        finally
        {
            await CleanupUserAsync(commenter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Delete_PostOwner_AllowsDeletingOthersComment()
    {
        var owner = await SeedUserAsync("pcc-dpo@example.com");
        var commenter = await SeedUserAsync("pcc-dpc@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-dp", isPublished: true);

            using var clientC = CreateAuthClient(commenter.Id, commenter.Email!);
            var tokenC = await GetCsrfAsync(clientC);
            clientC.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenC);
            var createResp = await clientC.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "yorum" });
            var created = await createResp.Content.ReadFromJsonAsync<CreateResp>();

            // Post sahibi başkasının yorumunu siler
            using var clientO = CreateAuthClient(owner.Id, owner.Email!);
            var tokenO = await GetCsrfAsync(clientO);
            clientO.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenO);
            var delResp = await clientO.PostAsync(
                $"/api/post-comments/{created!.commentId}/delete", null);

            delResp.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.PostComments.AnyAsync(c => c.Id == created.commentId))
                .Should().BeFalse();
        }
        finally
        {
            await CleanupUserAsync(commenter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Delete_UnauthorizedUser_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("pcc-duo@example.com");
        var commenter = await SeedUserAsync("pcc-duc@example.com");
        var stranger = await SeedUserAsync("pcc-dus@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "pcc-du", isPublished: true);

            using var clientC = CreateAuthClient(commenter.Id, commenter.Email!);
            var tokenC = await GetCsrfAsync(clientC);
            clientC.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenC);
            var createResp = await clientC.PostAsJsonAsync("/api/post-comments/create",
                new { postId, body = "yorum" });
            var created = await createResp.Content.ReadFromJsonAsync<CreateResp>();

            using var clientS = CreateAuthClient(stranger.Id, stranger.Email!);
            var tokenS = await GetCsrfAsync(clientS);
            clientS.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenS);
            var delResp = await clientS.PostAsync(
                $"/api/post-comments/{created!.commentId}/delete", null);

            delResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.PostComments.AnyAsync(c => c.Id == created.commentId))
                .Should().BeTrue("yetkisiz silme reddedildi, yorum durur");
        }
        finally
        {
            await CleanupUserAsync(stranger.Email!);
            await CleanupUserAsync(commenter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    private sealed class CreateResp
    {
        public string html { get; set; } = "";
        public int commentId { get; set; }
    }

    private sealed class UpdateResp
    {
        public string html { get; set; } = "";
    }
}
