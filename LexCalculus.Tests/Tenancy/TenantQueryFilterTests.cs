using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class TenantQueryFilterTests : SqlServerTestBase
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

    // SQL Server FK_CalculationHistories_Tenants_TenantId zorunlu. Tenant'ın
    // OwnerUserId FK'si de zorunlu (AspNetUsers). Circular FK staging:
    //   (a) user'ı TenantId=null ile seed et,
    //   (b) tenant'ı OwnerUserId=user.Id ile seed et,
    //   (c) user.TenantId = tenant.Id güncelle.
    // Sadece tenant Id'sine ihtiyacımız var; (c) adımını tamamlık için bırakıyoruz.
    private static async Task<int> SeedTenantWithOwnerAsync(
        ApplicationDbContext ctx, string suffix)
    {
        var user = new ApplicationUser
        {
            UserName = $"tenant-owner-{suffix}@x.com",
            NormalizedUserName = $"TENANT-OWNER-{suffix}@X.COM",
            Email = $"tenant-owner-{suffix}@x.com",
            NormalizedEmail = $"TENANT-OWNER-{suffix}@X.COM",
            FullName = $"Owner {suffix}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = $"Tenant {suffix}",
            Slug = $"tenant-{suffix.ToLowerInvariant()}",
            CreatedAt = DateTime.UtcNow,
            OwnerUserId = user.Id
        };
        ctx.Set<Tenant>().Add(tenant);
        await ctx.SaveChangesAsync();

        user.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        return tenant.Id;
    }

    [Fact]
    public async Task BireyselKullanici_KendiHesaplariniGorur_BaskaTenantHesaplariniGormez()
    {
        // Arrange — paylaşılan in-memory DB
        var options = _db.CreateOptions();
        const int userAId = 10;
        const int userBId = 20;

        int tenantOneId;
        await using (var seedTenant = _db.Create(options))
        {
            tenantOneId = await SeedTenantWithOwnerAsync(seedTenant, "1");
        }

        // Seed phase — NoOp context (filter UserId=null arar, ama IgnoreQueryFilters ile bypass)
        await using (var seed = _db.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(userAId, null, "private-A"),
                MakeHistory(userBId, tenantOneId, "shared-B"));
            await seed.SaveChangesAsync();
        }

        // Act — bireysel User A perspektifi (CurrentTenantId=null, CurrentUserId=A)
        await using var query = _db.Create(options,
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
        var options = _db.CreateOptions();
        const int userAId = 10;
        const int userBId = 20;
        const int userCId = 30;

        int tenantOneId, tenantTwoId;
        await using (var seedTenants = _db.Create(options))
        {
            tenantOneId = await SeedTenantWithOwnerAsync(seedTenants, "1");
            tenantTwoId = await SeedTenantWithOwnerAsync(seedTenants, "2");
        }

        await using (var seed = _db.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(userAId, tenantOneId, "shared-A"),
                MakeHistory(userAId, null, "private-A"),
                MakeHistory(userBId, tenantOneId, "shared-B"),
                MakeHistory(userBId, null, "private-B"),
                MakeHistory(userCId, tenantTwoId, "shared-C"));
            await seed.SaveChangesAsync();
        }

        // User A in Tenant 1 perspektifi
        await using var query = _db.Create(options,
            new TestTenantContext { CurrentTenantId = tenantOneId, CurrentUserId = userAId });
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
        var options = _db.CreateOptions();

        int tenantOneId, tenantTwoId;
        await using (var seedTenants = _db.Create(options))
        {
            tenantOneId = await SeedTenantWithOwnerAsync(seedTenants, "1");
            tenantTwoId = await SeedTenantWithOwnerAsync(seedTenants, "2");
        }

        await using (var seed = _db.Create(options))
        {
            seed.CalculationHistories.AddRange(
                MakeHistory(10, tenantOneId, "rec-1"),
                MakeHistory(20, tenantOneId, "rec-2"),
                MakeHistory(30, tenantTwoId, "rec-3"),
                MakeHistory(10, null, "rec-4"),
                MakeHistory(20, null, "rec-5"));
            await seed.SaveChangesAsync();
        }

        // Admin senaryosu: filter bypass, herhangi bir context bağlamı OK
        await using var query = _db.Create(options);
        var all = await query.CalculationHistories.IgnoreQueryFilters().ToListAsync();

        all.Should().HaveCount(5);
    }
}
