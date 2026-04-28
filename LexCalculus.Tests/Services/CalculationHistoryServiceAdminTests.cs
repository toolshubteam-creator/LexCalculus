using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Services;

public class CalculationHistoryServiceAdminTests
{
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
    public async Task GetAllPaginatedAsync_ReturnsAcrossUsers()
    {
        await using var ctx = TestDbContextFactory.Create();
        // 3 user, 2 satır her biri = 6 satır
        for (int u = 1; u <= 3; u++)
        {
            ctx.Set<CalculationHistory>().Add(MakeEntry(u));
            ctx.Set<CalculationHistory>().Add(MakeEntry(u));
        }
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var allPage = await svc.GetAllPaginatedAsync(page: 1, pageSize: 25);
        allPage.TotalCount.Should().Be(6);
        allPage.Items.Should().HaveCount(6);
        allPage.Items.Select(i => i.UserId).Distinct().Should().HaveCount(3);

        var userOnePage = await svc.GetAllPaginatedAsync(page: 1, pageSize: 25, userIdFilter: 1);
        userOnePage.TotalCount.Should().Be(2);
        userOnePage.Items.Should().OnlyContain(h => h.UserId == 1);
    }

    [Fact]
    public async Task GetUsersWithHistoryAsync_ReturnsDistinctSorted()
    {
        await using var ctx = TestDbContextFactory.Create();
        // 3 user, her birinin 2 satırı
        for (int u = 1; u <= 3; u++)
        {
            ctx.Set<CalculationHistory>().Add(MakeEntry(u));
            ctx.Set<CalculationHistory>().Add(MakeEntry(u));
        }
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        var ids = await svc.GetUsersWithHistoryAsync();

        ids.Should().BeEquivalentTo(new[] { 1, 2, 3 }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetByIdForAdminAsync_ReturnsAcrossOwners()
    {
        await using var ctx = TestDbContextFactory.Create();
        var entry = MakeEntry(userId: 1);
        ctx.Set<CalculationHistory>().Add(entry);
        await ctx.SaveChangesAsync();

        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        // Admin metot sahip kontrolü yapmaz — başka bir id'den de erişebilir
        var result = await svc.GetByIdForAdminAsync(entry.Id);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(1);
    }
}
