using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.ActivityLog;

public class ActivityLogServiceTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager(ApplicationDbContext ctx)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(x => x.Users).Returns(ctx.Users);
        return mgr;
    }

    private static ActivityLogService CreateService(
        ApplicationDbContext ctx,
        IHttpContextAccessor? httpAccessor = null,
        Mock<UserManager<ApplicationUser>>? umMock = null)
    {
        httpAccessor ??= new Mock<IHttpContextAccessor>().Object;
        var um = umMock?.Object ?? MockUserManager(ctx).Object;
        return new ActivityLogService(ctx, um, httpAccessor, NullLogger<ActivityLogService>.Instance);
    }

    [Fact]
    public async Task LogAsync_HappyPath_LogCreated()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        await svc.LogAsync(
            action: "User.Test",
            entityType: "ApplicationUser",
            entityId: 42,
            description: "Test açıklaması",
            metadata: new { Foo = "bar" });

        var row = await ctx.ActivityLogs.AsNoTracking().FirstAsync();
        row.Action.Should().Be("User.Test");
        row.EntityType.Should().Be("ApplicationUser");
        row.EntityId.Should().Be(42);
        row.Description.Should().Be("Test açıklaması");
        row.MetadataJson.Should().Contain("\"Foo\"").And.Contain("\"bar\"");
        row.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogAsync_NoUser_UserIdNull()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx); // HttpContextAccessor.HttpContext is null

        await svc.LogAsync(action: "System.Job");

        var row = await ctx.ActivityLogs.AsNoTracking().FirstAsync();
        row.UserId.Should().BeNull();
        row.UserName.Should().BeNull();
        row.IpAddress.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_LongMetadata_Truncated()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        var bigPayload = new { Blob = new string('x', 12_000) };
        await svc.LogAsync(action: "Test.Big", metadata: bigPayload);

        var row = await ctx.ActivityLogs.AsNoTracking().FirstAsync();
        row.MetadataJson.Should().NotBeNull();
        row.MetadataJson!.Length.Should().BeLessThanOrEqualTo(10_000 + "...[truncated]".Length);
        row.MetadataJson.Should().EndWith("...[truncated]");
    }

    [Fact]
    public async Task LogAsync_FailureDoesNotThrow()
    {
        // DbContext'i dispose ederek SaveChanges'in kesin fail etmesini sağla
        var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);
        await ctx.DisposeAsync();

        // Defansif try/catch sayesinde exception fırlamamalı
        var act = async () => await svc.LogAsync(action: "Test.Failure");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPaginatedAsync_FilterByAction_ReturnsMatching()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.ActivityLogs.AddRange(
            new Core.Entities.ActivityLog { Action = "User.Pasiflestir", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Core.Entities.ActivityLog { Action = "User.Aktiflestir", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Core.Entities.ActivityLog { Action = "User.Pasiflestir", CreatedAt = DateTime.UtcNow.AddMinutes(-3) }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPaginatedAsync(
            new ActivityLogFilter { Action = "User.Pasiflestir" }, page: 1, pageSize: 50);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(i => i.Action == "User.Pasiflestir");
    }

    [Fact]
    public async Task GetPaginatedAsync_FilterByDateRange_ReturnsMatching()
    {
        await using var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.ActivityLogs.AddRange(
            new Core.Entities.ActivityLog { Action = "A", CreatedAt = now.AddDays(-10) },
            new Core.Entities.ActivityLog { Action = "B", CreatedAt = now.AddDays(-1) },
            new Core.Entities.ActivityLog { Action = "C", CreatedAt = now }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPaginatedAsync(
            new ActivityLogFilter { FromDate = now.AddDays(-2), ToDate = now.AddSeconds(1) },
            page: 1, pageSize: 50);

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Action).Should().BeEquivalentTo(new[] { "B", "C" });
    }

    [Fact]
    public async Task GetPaginatedAsync_FilterByUserId_ReturnsMatching()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.ActivityLogs.AddRange(
            new Core.Entities.ActivityLog { Action = "X", UserId = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Core.Entities.ActivityLog { Action = "Y", UserId = 2, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Core.Entities.ActivityLog { Action = "Z", UserId = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-3) }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPaginatedAsync(
            new ActivityLogFilter { UserId = 1 }, page: 1, pageSize: 50);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(i => i.UserId == 1);
    }

    [Fact]
    public async Task GetPaginatedAsync_OrderedByCreatedAtDesc()
    {
        await using var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.ActivityLogs.AddRange(
            new Core.Entities.ActivityLog { Action = "Old", CreatedAt = now.AddHours(-3) },
            new Core.Entities.ActivityLog { Action = "Newest", CreatedAt = now },
            new Core.Entities.ActivityLog { Action = "Mid", CreatedAt = now.AddHours(-1) }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPaginatedAsync(new ActivityLogFilter(), page: 1, pageSize: 50);

        result.Items.Select(i => i.Action).Should().Equal("Newest", "Mid", "Old");
    }

    [Fact]
    public async Task GetPaginatedAsync_PageSize_LimitsResults()
    {
        await using var ctx = TestDbContextFactory.Create();
        for (var i = 0; i < 25; i++)
        {
            ctx.ActivityLogs.Add(new Core.Entities.ActivityLog
            {
                Action = $"Action.{i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPaginatedAsync(new ActivityLogFilter(), page: 1, pageSize: 10);

        result.TotalCount.Should().Be(25);
        result.Items.Should().HaveCount(10);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDetail()
    {
        await using var ctx = TestDbContextFactory.Create();
        var entry = new Core.Entities.ActivityLog
        {
            Action = "Test.Get",
            CreatedAt = DateTime.UtcNow,
            Description = "Detay testi",
            MetadataJson = "{\"k\":\"v\"}",
            IpAddress = "127.0.0.1",
            UserAgent = "xunit-runner/1.0"
        };
        ctx.ActivityLogs.Add(entry);
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var detail = await svc.GetByIdAsync(entry.Id);

        detail.Should().NotBeNull();
        detail!.Action.Should().Be("Test.Get");
        detail.Description.Should().Be("Detay testi");
        detail.MetadataJson.Should().Be("{\"k\":\"v\"}");
        detail.IpAddress.Should().Be("127.0.0.1");
        detail.UserAgent.Should().Be("xunit-runner/1.0");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        var detail = await svc.GetByIdAsync(99999);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task UserAdminService_SetActiveFalse_CreatesActivityLog()
    {
        // Entegrasyon: UserAdminService gerçek instance, ActivityLogService gerçek instance
        // SetActiveAsync(false) çağrısı sonrası ActivityLog tablosunda kayıt olmalı.
        await using var ctx = TestDbContextFactory.Create();

        var user = new ApplicationUser
        {
            Id = 7,
            UserName = "kullanici@example.com",
            NormalizedUserName = "KULLANICI@EXAMPLE.COM",
            Email = "kullanici@example.com",
            NormalizedEmail = "KULLANICI@EXAMPLE.COM",
            FullName = "Test Kullanıcı",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var umMock = MockUserManager(ctx);
        umMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        umMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        umMock.Setup(x => x.UpdateSecurityStampAsync(user)).ReturnsAsync(IdentityResult.Success);

        var httpAccessor = new Mock<IHttpContextAccessor>().Object;
        var activityLog = new ActivityLogService(
            ctx, umMock.Object, httpAccessor, NullLogger<ActivityLogService>.Instance);

        var emailMock = new Mock<Core.Email.IEmailService>().Object;
        var rendererMock = new Mock<Core.Email.IEmailTemplateRenderer>().Object;

        var userAdmin = new UserAdminService(
            ctx, umMock.Object, emailMock, rendererMock, activityLog,
            NullLogger<UserAdminService>.Instance);

        var ok = await userAdmin.SetActiveAsync(user.Id, false);
        ok.Should().BeTrue();

        var log = await ctx.ActivityLogs.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Action == "User.Pasiflestir" && a.EntityId == user.Id);
        log.Should().NotBeNull();
        log!.EntityType.Should().Be(nameof(ApplicationUser));
        log.Description.Should().Contain("kullanici@example.com");
    }
}
