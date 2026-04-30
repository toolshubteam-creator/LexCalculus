using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class HesaplarimScopeFilterTests
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

    private static (ApplicationDbContext ctx, CalculationHistoryService svc) Setup(int actAsUserId, int? actAsTenantId)
    {
        var tenantContext = new TestTenantContext { CurrentUserId = actAsUserId, CurrentTenantId = actAsTenantId };
        var ctx = TestDbContextFactory.Create(databaseName: null, tenantContext: tenantContext);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);
        return (ctx, svc);
    }

    [Fact]
    public async Task GetForUserAsync_ScopeMine_ReturnsOnlyPrivate()
    {
        // User1 (tenant=5) has 3 hesap: 1 özel, 1 paylaştığı, 1 ekibin paylaştığı (User2)
        var (ctx, svc) = Setup(actAsUserId: 1, actAsTenantId: 5);
        ctx.Users.AddRange(
            MakeUser(1, "u1@x.com", tenantId: 5),
            MakeUser(2, "u2@x.com", tenantId: 5));
        ctx.Set<CalculationHistory>().AddRange(
            MakeHistory(1, "kidem-tazminati", tenantId: null),    // mine, private
            MakeHistory(1, "ihbar-tazminati", tenantId: 5),       // mine, shared
            MakeHistory(2, "yillik-izin-ucreti", tenantId: 5));   // team
        await ctx.SaveChangesAsync();

        var page = await svc.GetForUserAsync(
            userId: 1, page: 1, pageSize: 25, scope: "mine");

        page.Items.Should().HaveCount(1);
        page.Items.Single().ToolSlug.Should().Be("kidem-tazminati");
    }

    [Fact]
    public async Task GetForUserAsync_ScopeTeam_ReturnsTeamSharedOnly()
    {
        var (ctx, svc) = Setup(actAsUserId: 1, actAsTenantId: 5);
        ctx.Users.AddRange(
            MakeUser(1, "u1@x.com", tenantId: 5),
            MakeUser(2, "u2@x.com", tenantId: 5));
        ctx.Set<CalculationHistory>().AddRange(
            MakeHistory(1, "kidem-tazminati", tenantId: null),    // mine private
            MakeHistory(1, "ihbar-tazminati", tenantId: 5),       // mine shared
            MakeHistory(2, "yillik-izin-ucreti", tenantId: 5),    // team
            MakeHistory(2, "fazla-mesai", tenantId: 5));          // team
        await ctx.SaveChangesAsync();

        var page = await svc.GetForUserAsync(
            userId: 1, page: 1, pageSize: 25, scope: "team");

        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(h => h.UserId == 2 && h.TenantId == 5);
    }
}
