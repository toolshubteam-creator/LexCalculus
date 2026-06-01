using FluentAssertions;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Content;

/// <summary>
/// Faz 6.11 (#17) — PostTagService.DecrementUsageForTagIdsAsync batch helper.
/// ContentReportService + UserAnonymizationService'teki inline floor-0 loop'un
/// tek-kaynak hâli: kullanım-başına azaltma, 0 floor, SaveChanges çağırmama.
/// </summary>
public sealed class TagUsageDecrementTests : SqlServerTestBase
{
    private async Task<(int a, int b)> SeedTwoTagsAsync(int usageA, int usageB)
    {
        var ctx = _db.Create();
        var a = new PostTag { Name = "Vergi", Slug = "vergi", UsageCount = usageA, CreatedAt = DateTime.UtcNow };
        var b = new PostTag { Name = "İcra", Slug = "icra", UsageCount = usageB, CreatedAt = DateTime.UtcNow };
        ctx.PostTags.AddRange(a, b);
        await ctx.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    [Fact]
    public async Task DecrementForTagIds_DecrementsOncePerOccurrence()
    {
        var (a, b) = await SeedTwoTagsAsync(usageA: 3, usageB: 2);

        var ctx = _db.Create();
        var svc = new PostTagService(ctx);
        // a iki kullanım, b bir kullanım (aynı tag farklı post'larda).
        await svc.DecrementUsageForTagIdsAsync(new[] { a, a, b });
        await ctx.SaveChangesAsync();

        var verify = _db.Create();
        (await verify.PostTags.FirstAsync(t => t.Id == a)).UsageCount.Should().Be(1, "3 - 2 kullanım");
        (await verify.PostTags.FirstAsync(t => t.Id == b)).UsageCount.Should().Be(1, "2 - 1 kullanım");
    }

    [Fact]
    public async Task DecrementForTagIds_FloorZero_DoesNotGoNegative()
    {
        var (a, _) = await SeedTwoTagsAsync(usageA: 1, usageB: 0);

        var ctx = _db.Create();
        var svc = new PostTagService(ctx);
        // a için iki azaltma istemi ama UsageCount 1 → 0'da durur (negatife inmez).
        await svc.DecrementUsageForTagIdsAsync(new[] { a, a });
        await ctx.SaveChangesAsync();

        var verify = _db.Create();
        (await verify.PostTags.FirstAsync(t => t.Id == a)).UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task DecrementForTagIds_EmptyOrUnknown_NoOp()
    {
        var (a, _) = await SeedTwoTagsAsync(usageA: 5, usageB: 5);

        var ctx = _db.Create();
        var svc = new PostTagService(ctx);
        await svc.DecrementUsageForTagIdsAsync(Array.Empty<int>());
        await svc.DecrementUsageForTagIdsAsync(new[] { 99999 });   // var olmayan id
        await ctx.SaveChangesAsync();

        var verify = _db.Create();
        (await verify.PostTags.FirstAsync(t => t.Id == a)).UsageCount.Should().Be(5);
    }

    [Fact]
    public async Task DecrementForTagIds_DoesNotSaveItself_CallerControlsUnitOfWork()
    {
        var (a, _) = await SeedTwoTagsAsync(usageA: 4, usageB: 4);

        var ctx = _db.Create();
        var svc = new PostTagService(ctx);
        await svc.DecrementUsageForTagIdsAsync(new[] { a });
        // SaveChanges ÇAĞRILMADI — ayrı context'ten okunan değer değişmemeli.
        var beforeSave = _db.Create();
        (await beforeSave.PostTags.FirstAsync(t => t.Id == a)).UsageCount
            .Should().Be(4, "helper kendi SaveChanges yapmaz");

        await ctx.SaveChangesAsync();
        var afterSave = _db.Create();
        (await afterSave.PostTags.FirstAsync(t => t.Id == a)).UsageCount
            .Should().Be(3, "çağıran SaveChanges sonrası persist olur");
    }
}
