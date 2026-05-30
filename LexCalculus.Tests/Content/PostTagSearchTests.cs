using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// Faz 6.6 — PostTagService.SearchByPrefixAsync (tag autocomplete, charter Karar 5).
/// </summary>
public class PostTagSearchTests : SqlServerTestBase
{
    private static void SeedTag(ApplicationDbContext ctx, string name, int usage)
        => ctx.PostTags.Add(new PostTag
        {
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            UsageCount = usage,
            CreatedAt = DateTime.UtcNow
        });

    [Fact]
    public async Task SearchByPrefixAsync_StartsWith_ReturnsPopularFirst()
    {
        await using var ctx = _db.Create();
        SeedTag(ctx, "hukuk", 5);
        SeedTag(ctx, "hukuki", 10);
        SeedTag(ctx, "huzur", 2);     // "huk" prefix DIŞINDA
        SeedTag(ctx, "ekonomi", 3);
        await ctx.SaveChangesAsync();

        var svc = new PostTagService(ctx);
        var results = await svc.SearchByPrefixAsync("huk", 10);

        results.Select(t => t.Name).Should().Equal("hukuki", "hukuk"); // UsageCount DESC
    }

    [Fact]
    public async Task SearchByPrefixAsync_CaseInsensitivePrefix_Matches()
    {
        await using var ctx = _db.Create();
        SeedTag(ctx, "hukuk", 5);
        await ctx.SaveChangesAsync();

        var svc = new PostTagService(ctx);
        var results = await svc.SearchByPrefixAsync("HUK", 10);

        results.Select(t => t.Name).Should().Contain("hukuk");
    }

    [Fact]
    public async Task SearchByPrefixAsync_ShortPrefix_ReturnsEmpty()
    {
        await using var ctx = _db.Create();
        SeedTag(ctx, "hukuk", 5);
        await ctx.SaveChangesAsync();

        var svc = new PostTagService(ctx);

        (await svc.SearchByPrefixAsync("h", 10)).Should().BeEmpty();
        (await svc.SearchByPrefixAsync("", 10)).Should().BeEmpty();
        (await svc.SearchByPrefixAsync("  ", 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByPrefixAsync_NoMatch_ReturnsEmpty()
    {
        await using var ctx = _db.Create();
        SeedTag(ctx, "hukuk", 5);
        await ctx.SaveChangesAsync();

        var svc = new PostTagService(ctx);
        (await svc.SearchByPrefixAsync("xyz", 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByPrefixAsync_TakeClampedTo20()
    {
        await using var ctx = _db.Create();
        for (int i = 0; i < 25; i++)
            SeedTag(ctx, $"auto{i:D2}", usage: i);
        await ctx.SaveChangesAsync();

        var svc = new PostTagService(ctx);
        var results = await svc.SearchByPrefixAsync("auto", take: 100);

        results.Should().HaveCount(20); // clamp 1-20
    }
}
