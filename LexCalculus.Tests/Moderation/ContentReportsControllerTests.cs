using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Moderation;

[Collection("AdminWebHost")]
public class ContentReportsControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public ContentReportsControllerTests(TestAuthWebApplicationFactory factory)
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
            UserName = email, Email = email, FullName = "Reporter",
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
        var reports = await ctx.ContentReports.Where(r => r.ReporterId == u.Id).ToListAsync();
        ctx.ContentReports.RemoveRange(reports);
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
    public async Task Create_Anonymous_ReturnsUnauthorizedOrRedirect()
    {
        using var client = CreateAnonClient();
        var response = await client.PostAsJsonAsync("/api/content-reports/create",
            new { targetType = 1, targetId = 1, reason = 1, note = (string?)null });
        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Valid_ReturnsOk()
    {
        var owner = await SeedUserAsync("crc-owner@example.com");
        var reporter = await SeedUserAsync("crc-reporter@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-valid", isPublished: true);

            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var response = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postId, reason = 1, note = (string?)null });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.ContentReports.AnyAsync(r =>
                r.ReporterId == reporter.Id && r.TargetId == postId
                && r.TargetType == ContentReportTargetType.Post)).Should().BeTrue();
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Create_InvalidEnum_ReturnsBadRequest()
    {
        var reporter = await SeedUserAsync("crc-invalid-r@example.com");
        try
        {
            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            // targetType=999 → invalid enum
            var response = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 999, targetId = 1, reason = 1, note = (string?)null });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
        }
    }

    [Fact]
    public async Task Create_DuplicateReport_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("crc-dup-o@example.com");
        var reporter = await SeedUserAsync("crc-dup-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-dup", isPublished: true);

            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var first = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postId, reason = 1, note = (string?)null });
            first.StatusCode.Should().Be(HttpStatusCode.OK);

            var second = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postId, reason = 2, note = (string?)null });
            second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Create_MissingCsrf_ReturnsBadRequest()
    {
        var owner = await SeedUserAsync("crc-csrf-o@example.com");
        var reporter = await SeedUserAsync("crc-csrf-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-csrf", isPublished: true);

            // CSRF header YOK
            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var response = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postId, reason = 1, note = (string?)null });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }
}
