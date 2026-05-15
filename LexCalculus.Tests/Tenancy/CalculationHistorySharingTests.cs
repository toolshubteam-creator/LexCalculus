using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Tenancy;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class CalculationHistorySharingTests : SqlServerTestBase
{
    private static ApplicationUser MakeUser(string suffix, string email, int? tenantId = null) =>
        new()
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenantId,
            SecurityStamp = Guid.NewGuid().ToString()
        };

    /// <summary>
    /// Tenant ↔ ApplicationUser circular FK staged seeder.
    /// (a) Add user with TenantId=null and save.
    /// (b) Add Tenant with OwnerUserId=user.Id (no explicit Tenant.Id) and save.
    /// (c) Set user.TenantId=tenant.Id and save.
    /// Returns (userId, tenantId).
    /// </summary>
    private static async Task<(int userId, int tenantId)> SeedUserAndTenantAsync(
        ApplicationDbContext ctx, string emailSuffix)
    {
        var user = MakeUser(emailSuffix, $"u{emailSuffix}@x.com", tenantId: null);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = $"Tenant {emailSuffix}",
            Slug = $"tenant-{emailSuffix.ToLowerInvariant()}",
            CreatedAt = DateTime.UtcNow,
            OwnerUserId = user.Id
        };
        ctx.Set<Tenant>().Add(tenant);
        await ctx.SaveChangesAsync();

        user.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        return (user.Id, tenant.Id);
    }

    private (ApplicationDbContext ctx, CalculationHistoryService svc) Setup(int? actAsUserId = null, int? actAsTenantId = null)
    {
        var tenantContext = new TestTenantContext { CurrentUserId = actAsUserId, CurrentTenantId = actAsTenantId };
        var ctx = _db.Create(databaseName: null, tenantContext: tenantContext);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        return (ctx, svc);
    }

    private sealed record DummyInput(string A);
    private sealed record DummyResult(decimal Total);

    [Fact]
    public async Task LogAsync_WithTenantId_TenantIdSet()
    {
        // Önce user + tenant seed et (no tenant context — global filter'a takılmasın).
        int userId, tenantId;
        {
            using var seedCtx = _db.Create();
            (userId, tenantId) = await SeedUserAndTenantAsync(seedCtx, "1");
        }

        var (ctx, svc) = Setup(actAsUserId: userId, actAsTenantId: tenantId);

        await svc.LogAsync(
            userId: userId,
            categorySlug: "is-hukuku",
            toolSlug: "kidem-tazminati",
            toolTitle: "Kıdem Tazminatı",
            input: new DummyInput("x"),
            result: new DummyResult(100m),
            totalAmount: 100m,
            unit: "TL",
            tenantId: tenantId);

        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync();
        entry.UserId.Should().Be(userId);
        entry.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task LogAsync_TenantIdNull_TenantIdNull()
    {
        int userId;
        {
            using var seedCtx = _db.Create();
            var user = MakeUser("1", "u1@x.com");
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();
            userId = user.Id;
        }

        var (ctx, svc) = Setup(actAsUserId: userId);

        await svc.LogAsync(
            userId: userId,
            categorySlug: "is-hukuku",
            toolSlug: "kidem-tazminati",
            toolTitle: "Kıdem Tazminatı",
            input: new DummyInput("x"),
            result: new DummyResult(100m),
            totalAmount: 100m,
            unit: "TL",
            tenantId: null);

        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync();
        entry.TenantId.Should().BeNull();
    }

    [Fact]
    public async Task SetSharingAsync_ShareTrue_TenantIdSetFromUser()
    {
        int userId, tenantId, historyId;
        {
            using var seedCtx = _db.Create();
            (userId, tenantId) = await SeedUserAndTenantAsync(seedCtx, "1");
            var history = new CalculationHistory
            {
                UserId = userId,
                CategorySlug = "is-hukuku",
                ToolSlug = "kidem-tazminati",
                ToolTitle = "K",
                InputJson = "{}",
                OutputJson = "{}",
                TenantId = null
            };
            seedCtx.Set<CalculationHistory>().Add(history);
            await seedCtx.SaveChangesAsync();
            historyId = history.Id;
        }

        var (ctx, svc) = Setup(actAsUserId: userId, actAsTenantId: tenantId);

        var ok = await svc.SetSharingAsync(historyId, requestedByUserId: userId, share: true);

        ok.Should().BeTrue();
        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync(h => h.Id == historyId);
        entry.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SetSharingAsync_ShareFalse_TenantIdCleared()
    {
        int userId, tenantId, historyId;
        {
            using var seedCtx = _db.Create();
            (userId, tenantId) = await SeedUserAndTenantAsync(seedCtx, "1");
            var history = new CalculationHistory
            {
                UserId = userId,
                CategorySlug = "is-hukuku",
                ToolSlug = "kidem-tazminati",
                ToolTitle = "K",
                InputJson = "{}",
                OutputJson = "{}",
                TenantId = tenantId
            };
            seedCtx.Set<CalculationHistory>().Add(history);
            await seedCtx.SaveChangesAsync();
            historyId = history.Id;
        }

        var (ctx, svc) = Setup(actAsUserId: userId, actAsTenantId: tenantId);

        var ok = await svc.SetSharingAsync(historyId, requestedByUserId: userId, share: false);

        ok.Should().BeTrue();
        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync(h => h.Id == historyId);
        entry.TenantId.Should().BeNull();
    }
}
