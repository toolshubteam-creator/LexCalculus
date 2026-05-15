using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Jobs.Tenancy;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Jobs;

public class ExpireInvitationsJobTests : SqlServerTestBase
{
    private static IActivityLogService CreateActivityLog(ApplicationDbContext ctx)
    {
        // Background job: HttpContext yok — IHttpContextAccessor null HttpContext döndürür
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!).Object;

        return new ActivityLogService(
            ctx, userManager, httpAccessor.Object,
            NullLogger<ActivityLogService>.Instance);
    }

    private static ExpireInvitationsJob CreateJob(ApplicationDbContext ctx)
        => new(ctx, CreateActivityLog(ctx), NullLogger<ExpireInvitationsJob>.Instance);

    private static ApplicationUser MakeUser(string suffix) => new()
    {
        UserName = $"inv-owner-{suffix}@x.com",
        NormalizedUserName = $"INV-OWNER-{suffix}@X.COM",
        Email = $"inv-owner-{suffix}@x.com",
        NormalizedEmail = $"INV-OWNER-{suffix}@X.COM",
        FullName = $"Owner {suffix}",
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    /// <summary>
    /// Tenant ↔ User circular FK staged seeder.
    /// </summary>
    private static async Task<(int userId, int tenantId)> SeedUserAndTenantAsync(
        ApplicationDbContext ctx, string suffix)
    {
        var user = MakeUser(suffix);
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

        return (user.Id, tenant.Id);
    }

    private static TenantInvitation MakeInvitation(
        string suffix, TenantInvitationStatus status, DateTime expiresAt,
        int tenantId, int invitedByUserId)
        => new()
        {
            TenantId = tenantId,
            Email = $"u{suffix}@x.com",
            Token = Guid.NewGuid().ToString("N"),
            InvitedByUserId = invitedByUserId,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = expiresAt
        };

    [Fact]
    public async Task ExecuteAsync_PendingExpiredInvitations_MarkedAsExpired()
    {
        await using var ctx = _db.Create();
        var (userId, tenantId) = await SeedUserAndTenantAsync(ctx, "1");
        var now = DateTime.UtcNow;
        var inv1Expired = MakeInvitation("1", TenantInvitationStatus.Pending, now.AddDays(-1), tenantId, userId);  // expired
        var inv2Future = MakeInvitation("2", TenantInvitationStatus.Pending, now.AddDays(+5), tenantId, userId);   // henüz değil
        ctx.TenantInvitations.AddRange(inv1Expired, inv2Future);
        await ctx.SaveChangesAsync();
        var inv1Id = inv1Expired.Id;
        var inv2Id = inv2Future.Id;

        await CreateJob(ctx).ExecuteAsync();

        var inv1 = await ctx.TenantInvitations.AsNoTracking().FirstAsync(i => i.Id == inv1Id);
        var inv2 = await ctx.TenantInvitations.AsNoTracking().FirstAsync(i => i.Id == inv2Id);
        inv1.Status.Should().Be(TenantInvitationStatus.Expired);
        inv2.Status.Should().Be(TenantInvitationStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_NoExpired_NoActivityLogCreated()
    {
        await using var ctx = _db.Create();
        var (userId, tenantId) = await SeedUserAndTenantAsync(ctx, "1");
        var now = DateTime.UtcNow;
        ctx.TenantInvitations.AddRange(
            MakeInvitation("1", TenantInvitationStatus.Pending, now.AddDays(+3), tenantId, userId),
            MakeInvitation("2", TenantInvitationStatus.Pending, now.AddDays(+7), tenantId, userId)
        );
        await ctx.SaveChangesAsync();

        await CreateJob(ctx).ExecuteAsync();

        var logs = await ctx.ActivityLogs.AsNoTracking().ToListAsync();
        logs.Should().BeEmpty();

        // Status'ler de değişmemeli
        (await ctx.TenantInvitations.AsNoTracking()
            .CountAsync(i => i.Status == TenantInvitationStatus.Pending))
            .Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyExpiredOrAcceptedOrCancelled_Untouched()
    {
        await using var ctx = _db.Create();
        var (userId, tenantId) = await SeedUserAndTenantAsync(ctx, "1");
        var pastExpiry = DateTime.UtcNow.AddDays(-30);
        var accepted = MakeInvitation("10", TenantInvitationStatus.Accepted, pastExpiry, tenantId, userId);
        var cancelled = MakeInvitation("11", TenantInvitationStatus.Cancelled, pastExpiry, tenantId, userId);
        var expired = MakeInvitation("12", TenantInvitationStatus.Expired, pastExpiry, tenantId, userId);
        ctx.TenantInvitations.AddRange(accepted, cancelled, expired);
        await ctx.SaveChangesAsync();
        var acceptedId = accepted.Id;
        var cancelledId = cancelled.Id;
        var expiredId = expired.Id;

        await CreateJob(ctx).ExecuteAsync();

        var rows = await ctx.TenantInvitations.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
        rows.Single(r => r.Id == acceptedId).Status.Should().Be(TenantInvitationStatus.Accepted);
        rows.Single(r => r.Id == cancelledId).Status.Should().Be(TenantInvitationStatus.Cancelled);
        rows.Single(r => r.Id == expiredId).Status.Should().Be(TenantInvitationStatus.Expired);

        // Job hiçbir kayıt değiştirmediği için ActivityLog da yazılmamalı
        var logs = await ctx.ActivityLogs.AsNoTracking().ToListAsync();
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredInvitations_ActivityLogCreated()
    {
        await using var ctx = _db.Create();
        var (userId, tenantId) = await SeedUserAndTenantAsync(ctx, "1");
        var past = DateTime.UtcNow.AddDays(-2);
        var i1 = MakeInvitation("101", TenantInvitationStatus.Pending, past, tenantId, userId);
        var i2 = MakeInvitation("102", TenantInvitationStatus.Pending, past, tenantId, userId);
        var i3 = MakeInvitation("103", TenantInvitationStatus.Pending, past, tenantId, userId);
        ctx.TenantInvitations.AddRange(i1, i2, i3);
        await ctx.SaveChangesAsync();
        var id1 = i1.Id;
        var id2 = i2.Id;
        var id3 = i3.Id;

        await CreateJob(ctx).ExecuteAsync();

        var logs = await ctx.ActivityLogs.AsNoTracking().ToListAsync();
        logs.Should().HaveCount(1);
        var log = logs[0];
        log.Action.Should().Be("System.ExpireInvitations");
        log.EntityType.Should().Be(nameof(TenantInvitation));
        log.UserId.Should().BeNull();
        log.UserName.Should().BeNull();
        log.MetadataJson.Should().NotBeNull();
        log.MetadataJson!.Should().Contain("\"ExpiredCount\":3");
        log.MetadataJson.Should().Contain(id1.ToString())
            .And.Contain(id2.ToString())
            .And.Contain(id3.ToString());
    }
}
