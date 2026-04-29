using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class TenantQueryFilterTests
{
    private static CalculationHistory MakeHistory(int userId, int? tenantId, string toolSlug) =>
        new()
        {
            UserId = userId,
            TenantId = tenantId,
            ToolSlug = toolSlug,
            ToolTitle = $"Tool {toolSlug}",
            CategorySlug = "test",
            InputJson = "{}",
            OutputJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task BireyselKullanici_KendiHesaplariniGorur_BaskaTenantHesaplariniGormez()
    {
        // Arrange — paylaşılan in-memory DB
        var options = TestDbContextFactory.CreateOptions();
        const int userAId = 10;
        const int userBId = 20;

        // Seed phase — NoOp context (filter UserId=null arar, ama IgnoreQueryFilters ile bypass)
        await using (var seed = TestDbContextFactory.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(userAId, null, "private-A"),
                MakeHistory(userBId, 1, "shared-B"));
            await seed.SaveChangesAsync();
        }

        // Act — bireysel User A perspektifi (CurrentTenantId=null, CurrentUserId=A)
        await using var query = TestDbContextFactory.Create(options,
            new TestTenantContext { CurrentUserId = userAId });
        var visible = await query.CalculationHistories.ToListAsync();

        // Assert — sadece A'nın null-tenant kaydı
        visible.Should().HaveCount(1);
        visible.Single().UserId.Should().Be(userAId);
        visible.Single().TenantId.Should().BeNull();
    }

    [Fact]
    public async Task TenantUyesi_TenantHesaplariniVeKendiOzelHesaplariniGorur()
    {
        var options = TestDbContextFactory.CreateOptions();
        const int userAId = 10;
        const int userBId = 20;
        const int userCId = 30;

        await using (var seed = TestDbContextFactory.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(userAId, 1, "shared-A"),
                MakeHistory(userAId, null, "private-A"),
                MakeHistory(userBId, 1, "shared-B"),
                MakeHistory(userBId, null, "private-B"),
                MakeHistory(userCId, 2, "shared-C"));
            await seed.SaveChangesAsync();
        }

        // User A in Tenant 1 perspektifi
        await using var query = TestDbContextFactory.Create(options,
            new TestTenantContext { CurrentTenantId = 1, CurrentUserId = userAId });
        var visible = await query.CalculationHistories.ToListAsync();

        // Beklenen: A'nın 2 (shared-A + private-A) + B'nin shared-B = 3
        visible.Should().HaveCount(3);
        visible.Should().Contain(h => h.ToolSlug == "shared-A");
        visible.Should().Contain(h => h.ToolSlug == "private-A");
        visible.Should().Contain(h => h.ToolSlug == "shared-B");
        visible.Should().NotContain(h => h.ToolSlug == "private-B");  // başka user'ın özel'i
        visible.Should().NotContain(h => h.ToolSlug == "shared-C");   // başka tenant
    }

    [Fact]
    public async Task IgnoreQueryFilters_TumKayitlariGorur()
    {
        var options = TestDbContextFactory.CreateOptions();

        await using (var seed = TestDbContextFactory.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(10, 1, "rec-1"),
                MakeHistory(20, 1, "rec-2"),
                MakeHistory(30, 2, "rec-3"),
                MakeHistory(10, null, "rec-4"),
                MakeHistory(20, null, "rec-5"));
            await seed.SaveChangesAsync();
        }

        // Admin senaryosu: filter bypass, herhangi bir context bağlamı OK
        await using var query = TestDbContextFactory.Create(options);
        var all = await query.CalculationHistories.IgnoreQueryFilters().ToListAsync();

        all.Should().HaveCount(5);
    }
}
