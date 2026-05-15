using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class HesaplarimScopeFilterTests : SqlServerTestBase
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

    private static CalculationHistory MakeHistory(int userId, string slug, int? tenantId)
        => new()
        {
            UserId = userId,
            CategorySlug = "is-hukuku",
            ToolSlug = slug,
            ToolTitle = slug,
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = tenantId
        };

    private (ApplicationDbContext ctx, CalculationHistoryService svc) Setup(int actAsUserId, int? actAsTenantId)
    {
        var tenantContext = new TestTenantContext { CurrentUserId = actAsUserId, CurrentTenantId = actAsTenantId };
        var ctx = _db.Create(databaseName: null, tenantContext: tenantContext);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        return (ctx, svc);
    }

    /// <summary>
    /// Tenant + 2 üye seed et. Circular FK staging:
    ///   1) İki user'ı TenantId=null ile ekle, save → user1.Id, user2.Id alınır.
    ///   2) Tenant'ı OwnerUserId=user1.Id ile ekle, save → tenant.Id alınır.
    ///   3) Her iki user'ın TenantId'sini tenant.Id'ye set et, save.
    /// </summary>
    private async Task<(int user1Id, int user2Id, int tenantId)> SeedTenantWithTwoUsersAsync()
    {
        using var seedCtx = _db.Create();
        var u1 = MakeUser("1", "u1@x.com", tenantId: null);
        var u2 = MakeUser("2", "u2@x.com", tenantId: null);
        seedCtx.Users.AddRange(u1, u2);
        await seedCtx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = "Test Tenant",
            Slug = "test-tenant",
            CreatedAt = DateTime.UtcNow,
            OwnerUserId = u1.Id
        };
        seedCtx.Set<Tenant>().Add(tenant);
        await seedCtx.SaveChangesAsync();

        u1.TenantId = tenant.Id;
        u2.TenantId = tenant.Id;
        await seedCtx.SaveChangesAsync();

        return (u1.Id, u2.Id, tenant.Id);
    }

    [Fact]
    public async Task GetForUserAsync_ScopeMine_ReturnsOnlyPrivate()
    {
        // User1 (tenant=X) has 3 hesap: 1 özel, 1 paylaştığı, 1 ekibin paylaştığı (User2)
        var (user1Id, user2Id, tenantId) = await SeedTenantWithTwoUsersAsync();

        var (ctx, svc) = Setup(actAsUserId: user1Id, actAsTenantId: tenantId);
        ctx.Set<CalculationHistory>().AddRange(
            MakeHistory(user1Id, "kidem-tazminati", tenantId: null),     // mine, private
            MakeHistory(user1Id, "ihbar-tazminati", tenantId: tenantId), // mine, shared
            MakeHistory(user2Id, "yillik-izin-ucreti", tenantId: tenantId)); // team
        await ctx.SaveChangesAsync();

        var page = await svc.GetForUserAsync(
            userId: user1Id, page: 1, pageSize: 25, scope: "mine");

        page.Items.Should().HaveCount(1);
        page.Items.Single().ToolSlug.Should().Be("kidem-tazminati");
    }

    [Fact]
    public async Task GetForUserAsync_ScopeTeam_ReturnsTeamSharedOnly()
    {
        var (user1Id, user2Id, tenantId) = await SeedTenantWithTwoUsersAsync();

        var (ctx, svc) = Setup(actAsUserId: user1Id, actAsTenantId: tenantId);
        ctx.Set<CalculationHistory>().AddRange(
            MakeHistory(user1Id, "kidem-tazminati", tenantId: null),     // mine private
            MakeHistory(user1Id, "ihbar-tazminati", tenantId: tenantId), // mine shared
            MakeHistory(user2Id, "yillik-izin-ucreti", tenantId: tenantId), // team
            MakeHistory(user2Id, "fazla-mesai", tenantId: tenantId));    // team
        await ctx.SaveChangesAsync();

        var page = await svc.GetForUserAsync(
            userId: user1Id, page: 1, pageSize: 25, scope: "team");

        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(h => h.UserId == user2Id && h.TenantId == tenantId);
    }
}
