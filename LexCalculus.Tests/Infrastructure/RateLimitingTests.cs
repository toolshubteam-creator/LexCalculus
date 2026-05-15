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

namespace LexCalculus.Tests.Infrastructure;

/// <summary>
/// Faz 5.2 — built-in rate limiter integration testleri.
/// Her test farklı user (farklı partition key) kullanır → testler arası
/// counter çakışması olmaz. Charter Karar 7.
/// </summary>
[Collection("AdminWebHost")]
public class RateLimitingTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public RateLimitingTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
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
            UserName = email, Email = email, FullName = "Rate Test",
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
        ctx.ContentReports.RemoveRange(
            await ctx.ContentReports.Where(r => r.ReporterId == u.Id).ToListAsync());
        ctx.PostComments.RemoveRange(
            await ctx.PostComments.Where(c => c.UserId == u.Id).ToListAsync());
        ctx.UserPosts.RemoveRange(
            await ctx.UserPosts.Where(p => p.UserId == u.Id).ToListAsync());
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        ctx.UserRoles.RemoveRange(ctx.UserRoles.Where(ur => ur.UserId == u.Id));
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
            UserId = userId, CategoryId = catId, Title = "T", Slug = slug,
            Body = "<p>x</p>", IsPublished = true, PublishedAt = now,
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
    public async Task ReportEndpoint_FirstFiveAllowed_SixthReturns429()
    {
        var owner = await SeedUserAsync("rl-rep-o@example.com");
        var reporter = await SeedUserAsync("rl-rep-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            // 6 farklı post seed (her şikayet farklı target → mükerrer engelli atlanır)
            var postIds = new int[6];
            for (int i = 0; i < 6; i++)
                postIds[i] = await SeedPostAsync(owner.Id, catId, $"rl-rep-post-{i}");

            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            var statuses = new List<HttpStatusCode>();
            for (int i = 0; i < 6; i++)
            {
                var resp = await client.PostAsJsonAsync("/api/content-reports/create",
                    new { targetType = 1, targetId = postIds[i], reason = 1, note = (string?)null });
                statuses.Add(resp.StatusCode);

                if (i == 5)
                {
                    // 6. istek 429 olmalı (report policy 5/saat)
                    resp.StatusCode.Should().Be((HttpStatusCode)429);
                    resp.Headers.Should().ContainKey("Retry-After");
                    var body = await resp.Content.ReadAsStringAsync();
                    body.Should().Contain("error");
                }
            }

            // İlk 5: 200 OK; 6.: 429
            statuses.Take(5).Should().AllSatisfy(s => s.Should().Be(HttpStatusCode.OK));
            statuses[5].Should().Be((HttpStatusCode)429);
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task ReportEndpoint_DifferentUsers_HavePartitionedLimits()
    {
        var owner = await SeedUserAsync("rl-part-o@example.com");
        var reporterA = await SeedUserAsync("rl-part-a@example.com");
        var reporterB = await SeedUserAsync("rl-part-b@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postIds = new int[6];
            for (int i = 0; i < 6; i++)
                postIds[i] = await SeedPostAsync(owner.Id, catId, $"rl-part-post-{i}");

            // User A: 5 şikayet (limit dolu)
            using var clientA = CreateAuthClient(reporterA.Id, reporterA.Email!);
            var tokenA = await GetCsrfAsync(clientA);
            clientA.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenA);

            for (int i = 0; i < 5; i++)
            {
                var resp = await clientA.PostAsJsonAsync("/api/content-reports/create",
                    new { targetType = 1, targetId = postIds[i], reason = 1, note = (string?)null });
                resp.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            // User B: ilk istek başarılı olmalı (kendi partition)
            using var clientB = CreateAuthClient(reporterB.Id, reporterB.Email!);
            var tokenB = await GetCsrfAsync(clientB);
            clientB.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenB);

            var respB = await clientB.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postIds[5], reason = 1, note = (string?)null });
            respB.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await CleanupUserAsync(reporterA.Email!);
            await CleanupUserAsync(reporterB.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }

    [Fact]
    public async Task RateLimit_429Response_HasJsonBodyAndRetryAfterHeader()
    {
        var owner = await SeedUserAsync("rl-fmt-o@example.com");
        var reporter = await SeedUserAsync("rl-fmt-r@example.com");
        var catId = await EnsureCategoryAsync();
        try
        {
            var postIds = new int[6];
            for (int i = 0; i < 6; i++)
                postIds[i] = await SeedPostAsync(owner.Id, catId, $"rl-fmt-post-{i}");

            using var client = CreateAuthClient(reporter.Id, reporter.Email!);
            var token = await GetCsrfAsync(client);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

            // 5 başarılı istek
            for (int i = 0; i < 5; i++)
            {
                await client.PostAsJsonAsync("/api/content-reports/create",
                    new { targetType = 1, targetId = postIds[i], reason = 1, note = (string?)null });
            }

            // 6. istek → 429
            var resp = await client.PostAsJsonAsync("/api/content-reports/create",
                new { targetType = 1, targetId = postIds[5], reason = 1, note = (string?)null });

            resp.StatusCode.Should().Be((HttpStatusCode)429);
            resp.Headers.Should().ContainKey("Retry-After");
            resp.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().Contain("error");
            body.Should().Contain("fazla"); // "Çok fazla istek..." mesajı, ASCII-safe substring
        }
        finally
        {
            await CleanupUserAsync(reporter.Email!);
            await CleanupUserAsync(owner.Email!);
        }
    }
}
