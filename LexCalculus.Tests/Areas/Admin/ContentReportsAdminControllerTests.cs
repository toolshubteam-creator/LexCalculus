using System.Net;
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

namespace LexCalculus.Tests.Areas.Admin;

/// <summary>
/// Admin moderasyon paneli (/admin/sikayetler) — Index, Detail, Dismiss, Action.
/// Faz 4.10 P2.
/// </summary>
[Collection("AdminWebHost")]
public class ContentReportsAdminControllerTests
    : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public ContentReportsAdminControllerTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(int userId = 9001, string email = "moderator-admin@local")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    private HttpClient CreateNonAdminClient(int userId, string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email, Email = email, FullName = "Test User",
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
        var reports = await ctx.ContentReports
            .Where(r => r.ReporterId == u.Id || r.ReviewedByUserId == u.Id).ToListAsync();
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

    private async Task<int> SeedPostAsync(int userId, int catId, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var p = new UserPost
        {
            UserId = userId, CategoryId = catId, Title = "Test makale",
            Slug = slug, Body = "<p>Sample body</p>",
            IsPublished = true, PublishedAt = now,
            CreatedAt = now, UpdatedAt = now
        };
        ctx.UserPosts.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task SeedReportAsync(
        int reporterId, int targetId,
        ContentReportTargetType targetType = ContentReportTargetType.Post,
        ContentReportReason reason = ContentReportReason.Spam)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.ContentReports.Add(new ContentReport
        {
            ReporterId = reporterId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            Status = ContentReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Index_AsAdmin_RendersList()
    {
        using var client = CreateAdminClient();
        var response = await client.GetAsync("/admin/sikayetler");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ikayet"); // "Şikayetler" — Türkçe encoding güvenli
        body.Should().Contain("bekleyen");
    }

    [Fact]
    public async Task Index_AsAnonymous_RedirectsOrUnauthorized()
    {
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/admin/sikayetler");

        ((int)response.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Index_AsNonAdmin_ReturnsForbidden()
    {
        var nonAdmin = await SeedUserAsync("crc-admin-non@example.com");
        try
        {
            using var client = CreateNonAdminClient(nonAdmin.Id, nonAdmin.Email!);
            var response = await client.GetAsync("/admin/sikayetler");

            ((int)response.StatusCode).Should().BeOneOf(
                (int)HttpStatusCode.Forbidden,
                (int)HttpStatusCode.Redirect,
                (int)HttpStatusCode.Unauthorized);
        }
        finally
        {
            await CleanupUserAsync(nonAdmin.Email!);
        }
    }

    [Fact]
    public async Task Detail_AsAdmin_RendersTargetAndReports()
    {
        var owner = await SeedUserAsync("crc-adm-d-o@example.com");
        var reporter = await SeedUserAsync("crc-adm-d-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-adm-d");
            await SeedReportAsync(reporter.Id, postId);

            using var client = CreateAdminClient();
            var response = await client.GetAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/{postId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Test makale"); // post title
            body.Should().Contain("Spam"); // reason label
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task Detail_NonExistentTarget_Returns404()
    {
        using var client = CreateAdminClient();
        var response = await client.GetAsync(
            $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/99999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dismiss_AsAdmin_RedirectsToIndex_AndUpdatesStatus()
    {
        var admin = await SeedUserAsync("crc-adm-dis-admin@example.com");
        var owner = await SeedUserAsync("crc-adm-dis-o@example.com");
        var reporter = await SeedUserAsync("crc-adm-dis-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-adm-dis");
            await SeedReportAsync(reporter.Id, postId);

            // ContentReport.ReviewedByUserId FK to AspNetUsers → admin must exist (SQL Server enforces).
            using var client = CreateAdminClient(admin.Id, admin.Email!);
            var token = await GetAntiforgeryTokenAsync(client,
                $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/{postId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("reviewNote", "Dismiss test")
            });

            var response = await client.PostAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/{postId}/dismiss", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/admin/sikayetler");

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var report = await ctx.ContentReports
                .FirstAsync(r => r.ReporterId == reporter.Id && r.TargetId == postId);
            report.Status.Should().Be(ContentReportStatus.Dismissed);
            report.ReviewNote.Should().Be("Dismiss test");
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
            await CleanupUserAsync(admin.Email!);
        }
    }

    [Fact]
    public async Task Action_AsAdmin_DeletesPost_AndUpdatesStatus()
    {
        var admin = await SeedUserAsync("crc-adm-act-admin@example.com");
        var owner = await SeedUserAsync("crc-adm-act-o@example.com");
        var reporter = await SeedUserAsync("crc-adm-act-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postId = await SeedPostAsync(owner.Id, catId, "crc-adm-act");
            await SeedReportAsync(reporter.Id, postId);

            // ContentReport.ReviewedByUserId FK to AspNetUsers → admin must exist (SQL Server enforces).
            using var client = CreateAdminClient(admin.Id, admin.Email!);
            var token = await GetAntiforgeryTokenAsync(client,
                $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/{postId}");

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("reviewNote", "Action test")
            });

            var response = await client.PostAsync(
                $"/admin/sikayetler/{(int)ContentReportTargetType.Post}/{postId}/action", form);

            ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

            using var scope = _factory.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserPosts.AnyAsync(p => p.Id == postId)).Should().BeFalse();
            var report = await ctx.ContentReports
                .FirstAsync(r => r.ReporterId == reporter.Id && r.TargetId == postId);
            report.Status.Should().Be(ContentReportStatus.Actioned);
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
            await CleanupUserAsync(admin.Email!);
        }
    }
}
