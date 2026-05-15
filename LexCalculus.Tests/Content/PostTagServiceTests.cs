using FluentAssertions;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

public class PostTagServiceTests : SqlServerTestBase
{
    private (PostTagService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new PostTagService(ctx);
        return (svc, ctx);
    }

    [Fact]
    public async Task GetOrCreateAsync_NewTag_Creates()
    {
        var (svc, ctx) = Setup();

        var tag = await svc.GetOrCreateAsync("İş Hukuku");

        tag.Id.Should().BeGreaterThan(0);
        tag.Slug.Should().Be("is-hukuku");
        tag.Name.Should().Be("İş Hukuku");
        tag.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingTag_ReturnsSameRow()
    {
        var (svc, ctx) = Setup();
        var first = await svc.GetOrCreateAsync("KVKK");

        var second = await svc.GetOrCreateAsync("KVKK");

        second.Id.Should().Be(first.Id);
        (await ctx.PostTags.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_TurkishCharacters_NormalizesToSlug()
    {
        var (svc, _) = Setup();
        var tag = await svc.GetOrCreateAsync("Çocuk Hakları");
        tag.Slug.Should().Be("cocuk-haklari");
    }

    [Fact]
    public async Task GetOrCreateAsync_CaseInsensitive_ReturnsExisting()
    {
        // "İş Hukuku" → slug "is-hukuku"; "iş hukuku" da aynı slug → mevcut tag.
        var (svc, ctx) = Setup();
        var upper = await svc.GetOrCreateAsync("İş Hukuku");
        var lower = await svc.GetOrCreateAsync("iş hukuku");

        lower.Id.Should().Be(upper.Id);
        (await ctx.PostTags.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_TruncatesNameTo30Chars()
    {
        var (svc, _) = Setup();
        var longName = new string('a', 50);
        var tag = await svc.GetOrCreateAsync(longName);
        tag.Name.Length.Should().Be(30);
    }

    [Fact]
    public async Task GetOrCreateAsync_EmptyName_Throws()
    {
        var (svc, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetOrCreateAsync("   "));
    }

    [Fact]
    public async Task IncrementUsageAsync_IncreasesCount()
    {
        var (svc, ctx) = Setup();
        var tag = await svc.GetOrCreateAsync("test");

        await svc.IncrementUsageAsync(tag.Id);
        await svc.IncrementUsageAsync(tag.Id);

        var refreshed = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Id == tag.Id);
        refreshed.UsageCount.Should().Be(2);
    }

    [Fact]
    public async Task DecrementUsageAsync_DoesNotGoBelowZero()
    {
        var (svc, ctx) = Setup();
        var tag = await svc.GetOrCreateAsync("test");

        await svc.DecrementUsageAsync(tag.Id);

        var refreshed = await ctx.PostTags.AsNoTracking().FirstAsync(t => t.Id == tag.Id);
        refreshed.UsageCount.Should().Be(0, "filtre UsageCount > 0 ile negatif önlenir");
    }

    [Fact]
    public async Task GetPopularAsync_OrdersByUsageCountDesc_TakesLimit()
    {
        var (svc, _) = Setup();
        var t1 = await svc.GetOrCreateAsync("alpha");
        var t2 = await svc.GetOrCreateAsync("beta");
        var t3 = await svc.GetOrCreateAsync("gamma");
        var t4 = await svc.GetOrCreateAsync("delta");

        for (var i = 0; i < 5; i++) await svc.IncrementUsageAsync(t2.Id);
        for (var i = 0; i < 3; i++) await svc.IncrementUsageAsync(t1.Id);
        for (var i = 0; i < 1; i++) await svc.IncrementUsageAsync(t3.Id);
        // t4 = 0, dahil edilmemeli

        var top2 = await svc.GetPopularAsync(2);

        top2.Should().HaveCount(2);
        top2[0].Slug.Should().Be("beta");
        top2[1].Slug.Should().Be("alpha");
    }

    [Fact]
    public async Task GetPopularAsync_ExcludesZeroUsage()
    {
        var (svc, _) = Setup();
        await svc.GetOrCreateAsync("alpha");

        var popular = await svc.GetPopularAsync();

        popular.Should().BeEmpty("UsageCount=0 olanlar listede olmaz");
    }
}
