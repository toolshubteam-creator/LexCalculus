using FluentAssertions;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Notifications;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Notifications;

public class NotificationServiceTests
{
    private static NotificationService CreateService(LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) =>
        new NotificationService(ctx, NullLogger<NotificationService>.Instance);

    [Fact]
    public async Task CreateAsync_WithoutDedup_PersistsNotification()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        var n = await svc.CreateAsync(NotificationType.SystemAlert, userId: 1, title: "Title", body: "Body");

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
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        var first = await svc.CreateAsync(
            NotificationType.DataFreshness, userId: 1, title: "T1", body: "B1",
            relatedEntityType: "FormulaParameter", relatedEntityId: 42,
            dedupWindow: TimeSpan.FromDays(7));

        var second = await svc.CreateAsync(
            NotificationType.DataFreshness, userId: 1, title: "T2", body: "B2",
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
        await using var ctx = TestDbContextFactory.Create();

        // Old notification beyond the 7-day window
        ctx.Notifications.Add(new Notification
        {
            UserId = 1,
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
            NotificationType.DataFreshness, userId: 1, title: "New", body: "New",
            relatedEntityType: "FormulaParameter", relatedEntityId: 42,
            dedupWindow: TimeSpan.FromDays(7));

        newOne.Should().NotBeNull();
        var dbCount = await ctx.Notifications.CountAsync();
        dbCount.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsOnlyUnread()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Notifications.AddRange(
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "A", Body = "A", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "B", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "C", Body = "C", IsRead = true, ReadAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var count = await svc.GetUnreadCountAsync(userId: 1);

        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsIsReadAndReadAt()
    {
        await using var ctx = TestDbContextFactory.Create();
        var n = new Notification
        {
            UserId = 1,
            Type = NotificationType.SystemAlert,
            Title = "T",
            Body = "B",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.MarkAsReadAsync(n.Id, userId: 1);

        var updated = await ctx.Notifications.FirstAsync(x => x.Id == n.Id);
        updated.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
        updated.ReadAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkAsReadAsync_ThrowsWhenNotOwner()
    {
        await using var ctx = TestDbContextFactory.Create();
        var n = new Notification
        {
            UserId = 1,
            Type = NotificationType.SystemAlert,
            Title = "T",
            Body = "B",
            CreatedAt = DateTime.UtcNow
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var act = async () => await svc.MarkAsReadAsync(n.Id, userId: 2);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyUnreadOfThatUser()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Notifications.AddRange(
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "U1A", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "U1B", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 1, Type = NotificationType.SystemAlert, Title = "U1Read", Body = "B", IsRead = true, ReadAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow },
            new Notification { UserId = 2, Type = NotificationType.SystemAlert, Title = "U2", Body = "B", IsRead = false, CreatedAt = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var marked = await svc.MarkAllAsReadAsync(userId: 1);

        marked.Should().Be(2);

        var u1Unread = await svc.GetUnreadCountAsync(userId: 1);
        var u2Unread = await svc.GetUnreadCountAsync(userId: 2);
        u1Unread.Should().Be(0);
        u2Unread.Should().Be(1);
    }
}
