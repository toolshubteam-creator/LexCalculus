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

public class CalculationHistorySharingTests
{
    private static ApplicationUser MakeUser(int id, string email, int? tenantId = null) =>
        new()
        {
            Id = id,
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

    private static (ApplicationDbContext ctx, CalculationHistoryService svc) Setup(int? actAsUserId = null, int? actAsTenantId = null)
    {
        var tenantContext = new TestTenantContext { CurrentUserId = actAsUserId, CurrentTenantId = actAsTenantId };
        var ctx = TestDbContextFactory.Create(databaseName: null, tenantContext: tenantContext);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        return (ctx, svc);
    }

    private sealed record DummyInput(string A);
    private sealed record DummyResult(decimal Total);

    [Fact]
    public async Task LogAsync_WithTenantId_TenantIdSet()
    {
        var (ctx, svc) = Setup(actAsUserId: 1, actAsTenantId: 5);
        ctx.Users.Add(MakeUser(1, "u@x.com", tenantId: 5));
        await ctx.SaveChangesAsync();

        await svc.LogAsync(
            userId: 1,
            categorySlug: "is-hukuku",
            toolSlug: "kidem-tazminati",
            toolTitle: "Kıdem Tazminatı",
            input: new DummyInput("x"),
            result: new DummyResult(100m),
            totalAmount: 100m,
            unit: "TL",
            tenantId: 5);

        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync();
        entry.UserId.Should().Be(1);
        entry.TenantId.Should().Be(5);
    }

    [Fact]
    public async Task LogAsync_TenantIdNull_TenantIdNull()
    {
        var (ctx, svc) = Setup(actAsUserId: 1);
        ctx.Users.Add(MakeUser(1, "u@x.com"));
        await ctx.SaveChangesAsync();

        await svc.LogAsync(
            userId: 1,
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
        var (ctx, svc) = Setup(actAsUserId: 1, actAsTenantId: 5);
        ctx.Users.Add(MakeUser(1, "u@x.com", tenantId: 5));
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 1,
            CategorySlug = "is-hukuku",
            ToolSlug = "kidem-tazminati",
            ToolTitle = "K",
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = null
        });
        await ctx.SaveChangesAsync();
        var historyId = await ctx.Set<CalculationHistory>().AsAdminQuery().Select(h => h.Id).FirstAsync();

        var ok = await svc.SetSharingAsync(historyId, requestedByUserId: 1, share: true);

        ok.Should().BeTrue();
        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync(h => h.Id == historyId);
        entry.TenantId.Should().Be(5);
    }

    [Fact]
    public async Task SetSharingAsync_ShareFalse_TenantIdCleared()
    {
        var (ctx, svc) = Setup(actAsUserId: 1, actAsTenantId: 5);
        ctx.Users.Add(MakeUser(1, "u@x.com", tenantId: 5));
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 1,
            CategorySlug = "is-hukuku",
            ToolSlug = "kidem-tazminati",
            ToolTitle = "K",
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = 5
        });
        await ctx.SaveChangesAsync();
        var historyId = await ctx.Set<CalculationHistory>().AsAdminQuery().Select(h => h.Id).FirstAsync();

        var ok = await svc.SetSharingAsync(historyId, requestedByUserId: 1, share: false);

        ok.Should().BeTrue();
        var entry = await ctx.Set<CalculationHistory>().AsAdminQuery().FirstAsync(h => h.Id == historyId);
        entry.TenantId.Should().BeNull();
    }
}
