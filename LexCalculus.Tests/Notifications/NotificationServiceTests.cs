using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Notifications;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Notifications;

public class NotificationServiceTests : SqlServerTestBase
{
    private static NotificationService CreateService(LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) =>
        new NotificationService(ctx, NullLogger<NotificationService>.Instance);

    // SQL Server FK_Notifications_AspNetUsers_UserId zorunlu — Notification eklenmeden
    // önce ilgili ApplicationUser seed edilmeli.
    private static async Task<ApplicationUser> SeedUserAsync(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx, string suffix)
    {
        var user = new ApplicationUser
        {
            UserName = $"u{suffix}@x.com",
            NormalizedUserName = $"U{suffix}@X.COM",
            Email = $"u{suffix}@x.com",
            NormalizedEmail = $"U{suffix}@X.COM",
            FullName = $"U{suffix}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateAsync_WithoutDedup_PersistsNotification()
    {
        await using var ctx = _db.Create();
        var user = await SeedUserAsync(ctx, "create");
        var svc = CreateService(ctx);

        var n = await svc.CreateAsync(NotificationType.SystemAlert, userId: user.Id, title: "Title", body: "Body");

        n.Should().NotBeNull();
        n!.Id.Should().BeGreaterThan(0);
        n.Type.Should().Be(NotificationType.SystemAlert);
        n.IsRead.Should().BeFalse();
        n.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var dbCount = await ctx.Notifications.CountAsync();
        dbCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithDedupWindow_PreventsDuplicate()
    {
        await using var ctx = _db.Create();
        var user = await SeedUserAsync(ctx, "dedup");
        var svc = CreateService(ctx);

        var first = await svc.CreateAsync(
            NotificationType.DataFreshness, userId: user.Id, title: "T1", body: "B1",
            relatedEntityType: "FormulaParameter", relatedEntityId: 42,
            dedupWindow: TimeSpan.FromDays(7));

        var second = await svc.CreateAsync(
            NotificationType.DataFreshness, userId: user.Id, title: "T2", body: "B2",
            relatedEntityType: "FormulaParameter", relatedEntityId: 42,
            dedupWindow: TimeSpan.FromDays(7));

        first.Should().NotBeNull();
        second.Should().BeNull();

        var dbCount = await ctx.Notifications.CountAsync();
        dbCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithDedupWindow_AllowsAfterWindow()
    {
        await using var ctx = _db.Create();
        var user = await SeedUserAsync(ctx, "afterwin");

        // Old notification beyond the 7-day window
        ctx.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Type = NotificationType.DataFreshness,
            Title = "Old",
            Body = "Old",
            RelatedEntityType = "FormulaParameter",
            RelatedEntityId = 42,
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var newOne = await svc.CreateAsync(
            NotificationType.DataFreshness, userId: user.Id, title: "New", body: "New",
            relatedEntityType: "FormulaParameter", relatedEntityId: 42,
            dedupWindow: TimeSpan.FromDays(7));

        newOne.Should().NotBeNull();
        var dbCount = await ctx.Notifications.CountAsync();
        dbCount.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsOnlyUnread()
    {
        await using var ctx = _db.Create();
        var user = await SeedUserAsync(ctx, "unread");
        ctx.Notifications.AddRange(
            new Notification { UserId = user.Id, Type = NotificationType.SystemAlert, Title = "A", Body = "A", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = user.Id, Type = NotificationType.SystemAlert, Title = "B", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = user.Id, Type = NotificationType.SystemAlert, Title = "C", Body = "C", IsRead = true, ReadAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var count = await svc.GetUnreadCountAsync(userId: user.Id);

        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsIsReadAndReadAt()
    {
        await using var ctx = _db.Create();
        var user = await SeedUserAsync(ctx, "mark");
        var n = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.SystemAlert,
            Title = "T",
            Body = "B",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.MarkAsReadAsync(n.Id, userId: user.Id);

        var updated = await ctx.Notifications.FirstAsync(x => x.Id == n.Id);
        updated.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
        updated.ReadAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkAsReadAsync_ThrowsWhenNotOwner()
    {
        await using var ctx = _db.Create();
        var owner = await SeedUserAsync(ctx, "owner");
        var other = await SeedUserAsync(ctx, "other");
        var n = new Notification
        {
            UserId = owner.Id,
            Type = NotificationType.SystemAlert,
            Title = "T",
            Body = "B",
            CreatedAt = DateTime.UtcNow
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = async () => await svc.MarkAsReadAsync(n.Id, userId: other.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyUnreadOfThatUser()
    {
        await using var ctx = _db.Create();
        var u1 = await SeedUserAsync(ctx, "u1");
        var u2 = await SeedUserAsync(ctx, "u2");
        ctx.Notifications.AddRange(
            new Notification { UserId = u1.Id, Type = NotificationType.SystemAlert, Title = "U1A", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = u1.Id, Type = NotificationType.SystemAlert, Title = "U1B", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = u1.Id, Type = NotificationType.SystemAlert, Title = "U1Read", Body = "B", IsRead = true, ReadAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = u2.Id, Type = NotificationType.SystemAlert, Title = "U2", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var marked = await svc.MarkAllAsReadAsync(userId: u1.Id);

        marked.Should().Be(2);

        var u1Unread = await svc.GetUnreadCountAsync(userId: u1.Id);
        var u2Unread = await svc.GetUnreadCountAsync(userId: u2.Id);
        u1Unread.Should().Be(0);
        u2Unread.Should().Be(1);
    }
}
