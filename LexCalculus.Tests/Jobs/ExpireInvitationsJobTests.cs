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

public class ExpireInvitationsJobTests
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

    private static TenantInvitation MakeInvitation(
        int id, TenantInvitationStatus status, DateTime expiresAt, int tenantId = 1)
        => new()
        {
            Id = id,
            TenantId = tenantId,
            Email = $"u{id}@x.com",
            Token = Guid.NewGuid().ToString("N"),
            InvitedByUserId = 1,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = expiresAt
        };

    [Fact]
    public async Task ExecuteAsync_PendingExpiredInvitations_MarkedAsExpired()
    {
        await using var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.TenantInvitations.AddRange(
            MakeInvitation(1, TenantInvitationStatus.Pending, now.AddDays(-1)),  // expired
            MakeInvitation(2, TenantInvitationStatus.Pending, now.AddDays(+5))   // henüz değil
        );
        await ctx.SaveChangesAsync();

        await CreateJob(ctx).ExecuteAsync();

        var inv1 = await ctx.TenantInvitations.AsNoTracking().FirstAsync(i => i.Id == 1);
        var inv2 = await ctx.TenantInvitations.AsNoTracking().FirstAsync(i => i.Id == 2);
        inv1.Status.Should().Be(TenantInvitationStatus.Expired);
        inv2.Status.Should().Be(TenantInvitationStatus.Pending);
    }

    [Fact]
    public async Task ExecuteAsync_NoExpired_NoActivityLogCreated()
    {
        await using var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.TenantInvitations.AddRange(
            MakeInvitation(1, TenantInvitationStatus.Pending, now.AddDays(+3)),
            MakeInvitation(2, TenantInvitationStatus.Pending, now.AddDays(+7))
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
        await using var ctx = TestDbContextFactory.Create();
        var pastExpiry = DateTime.UtcNow.AddDays(-30);
        ctx.TenantInvitations.AddRange(
            MakeInvitation(10, TenantInvitationStatus.Accepted, pastExpiry),
            MakeInvitation(11, TenantInvitationStatus.Cancelled, pastExpiry),
            MakeInvitation(12, TenantInvitationStatus.Expired, pastExpiry)
        );
        await ctx.SaveChangesAsync();

        await CreateJob(ctx).ExecuteAsync();

        var rows = await ctx.TenantInvitations.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
        rows[0].Status.Should().Be(TenantInvitationStatus.Accepted);
        rows[1].Status.Should().Be(TenantInvitationStatus.Cancelled);
        rows[2].Status.Should().Be(TenantInvitationStatus.Expired);

        // Job hiçbir kayıt değiştirmediği için ActivityLog da yazılmamalı
        var logs = await ctx.ActivityLogs.AsNoTracking().ToListAsync();
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredInvitations_ActivityLogCreated()
    {
        await using var ctx = TestDbContextFactory.Create();
        var past = DateTime.UtcNow.AddDays(-2);
        ctx.TenantInvitations.AddRange(
            MakeInvitation(101, TenantInvitationStatus.Pending, past),
            MakeInvitation(102, TenantInvitationStatus.Pending, past),
            MakeInvitation(103, TenantInvitationStatus.Pending, past)
        );
        await ctx.SaveChangesAsync();

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
        log.MetadataJson.Should().Contain("101").And.Contain("102").And.Contain("103");
    }
}
