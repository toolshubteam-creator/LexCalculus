using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Services;

public class CalculationHistoryServiceReadTests
{
    /// <summary>
    /// Audit interceptor'sız context — özelleştirilmiş CreatedAt seed'i için.
    /// In-memory DB aynı isimde olduğu için service tarafı (TestDbContextFactory)
    /// aynı veriyi görür.
    /// </summary>
    private static ApplicationDbContext CreateNoAuditCtx(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CalculationHistory MakeEntry(int userId, string toolSlug = "kidem-tazminati") =>
        new()
        {
            UserId = userId,
            CategorySlug = "is-hukuku",
            ToolSlug = toolSlug,
            ToolTitle = toolSlug,
            InputJson = "{}",
            OutputJson = "{}"
        };

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyForGivenUser()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<CalculationHistory>().AddRange(
            MakeEntry(1), MakeEntry(1), MakeEntry(1),
            MakeEntry(2), MakeEntry(2), MakeEntry(2)
        );
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        var page = await svc.GetForUserAsync(userId: 1, page: 1, pageSize: 25);

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(3);
        page.Items.Should().OnlyContain(h => h.UserId == 1);
    }

    [Fact]
    public async Task GetForUserAsync_PaginatesCorrectly()
    {
        await using var ctx = TestDbContextFactory.Create();
        for (int i = 0; i < 30; i++) ctx.Set<CalculationHistory>().Add(MakeEntry(1));
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var p1 = await svc.GetForUserAsync(userId: 1, page: 1, pageSize: 25);
        p1.Items.Should().HaveCount(25);
        p1.TotalCount.Should().Be(30);
        p1.TotalPages.Should().Be(2);
        p1.HasNext.Should().BeTrue();
        p1.HasPrevious.Should().BeFalse();

        var p2 = await svc.GetForUserAsync(userId: 1, page: 2, pageSize: 25);
        p2.Items.Should().HaveCount(5);
        p2.HasNext.Should().BeFalse();
        p2.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public async Task GetForUserAsync_FilterByToolSlug()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<CalculationHistory>().AddRange(
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "ihbar-tazminati"),
            MakeEntry(1, "ihbar-tazminati")
        );
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var page = await svc.GetForUserAsync(
            userId: 1, page: 1, pageSize: 25,
            toolSlugFilter: "kidem-tazminati");

        page.TotalCount.Should().Be(3);
        page.Items.Should().OnlyContain(h => h.ToolSlug == "kidem-tazminati");
    }

    [Fact]
    public async Task GetForUserAsync_FilterByDateRange()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Seed: 3 satır son hafta içinde, 2 satır 30 gün eski.
        // Audit interceptor'ı bypass eden context ile özel CreatedAt set ediliyor.
        using (var seedCtx = CreateNoAuditCtx(dbName))
        {
            seedCtx.Set<CalculationHistory>().AddRange(
                new CalculationHistory { UserId = 1, CategorySlug = "is-hukuku",
                    ToolSlug = "kidem", ToolTitle = "K", InputJson = "{}", OutputJson = "{}",
                    CreatedAt = now.AddDays(-1) },
                new CalculationHistory { UserId = 1, CategorySlug = "is-hukuku",
                    ToolSlug = "kidem", ToolTitle = "K", InputJson = "{}", OutputJson = "{}",
                    CreatedAt = now.AddDays(-3) },
                new CalculationHistory { UserId = 1, CategorySlug = "is-hukuku",
                    ToolSlug = "kidem", ToolTitle = "K", InputJson = "{}", OutputJson = "{}",
                    CreatedAt = now.AddDays(-5) },
                new CalculationHistory { UserId = 1, CategorySlug = "is-hukuku",
                    ToolSlug = "kidem", ToolTitle = "K", InputJson = "{}", OutputJson = "{}",
                    CreatedAt = now.AddDays(-30) },
                new CalculationHistory { UserId = 1, CategorySlug = "is-hukuku",
                    ToolSlug = "kidem", ToolTitle = "K", InputJson = "{}", OutputJson = "{}",
                    CreatedAt = now.AddDays(-45) }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = TestDbContextFactory.Create(dbName);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var page = await svc.GetForUserAsync(
            userId: 1, page: 1, pageSize: 25,
            startDateUtc: now.AddDays(-7));

        page.TotalCount.Should().Be(3);   // sadece son hafta içindekiler
    }

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNullForOtherOwner()
    {
        await using var ctx = TestDbContextFactory.Create();
        var entry = MakeEntry(userId: 1);
        ctx.Set<CalculationHistory>().Add(entry);
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var ownResult = await svc.GetByIdForUserAsync(entry.Id, userId: 1);
        ownResult.Should().NotBeNull();

        var foreignResult = await svc.GetByIdForUserAsync(entry.Id, userId: 2);
        foreignResult.Should().BeNull();
    }

    [Fact]
    public async Task GetUsedToolSlugsForUserAsync_ReturnsDistinctSorted()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<CalculationHistory>().AddRange(
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "kidem-tazminati"),
            MakeEntry(1, "ihbar-tazminati"),
            MakeEntry(1, "ihbar-tazminati"),
            MakeEntry(2, "yasal-faiz")   // başka kullanıcı, dahil edilmemeli
        );
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        var slugs = await svc.GetUsedToolSlugsForUserAsync(userId: 1);

        slugs.Should().BeEquivalentTo(new[] { "ihbar-tazminati", "kidem-tazminati" },
            o => o.WithStrictOrdering());
    }
}
