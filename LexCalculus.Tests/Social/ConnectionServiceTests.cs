using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Social;

public class ConnectionServiceTests
{
    private static (ConnectionService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = new ConnectionService(ctx, new NullActivityLogService(),
            new NullNotificationService(),
            new UserBlockService(ctx, new NullActivityLogService()));
        return (svc, ctx);
    }

    private static (ConnectionService svc, ApplicationDbContext ctx, RecordingNotificationService notif) SetupRecording()
    {
        var ctx = TestDbContextFactory.Create();
        var notif = new RecordingNotificationService();
        var svc = new ConnectionService(ctx, new NullActivityLogService(), notif,
            new UserBlockService(ctx, new NullActivityLogService()));
        return (svc, ctx, notif);
    }

    private static ApplicationUser MakeUser(int id, bool isActive = true) => new()
    {
        Id = id,
        UserName = $"u{id}@x.com",
        NormalizedUserName = $"U{id}@X.COM",
        Email = $"u{id}@x.com",
        NormalizedEmail = $"U{id}@X.COM",
        FullName = $"User {id}",
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static async Task SeedUsersAsync(ApplicationDbContext ctx, params int[] userIds)
    {
        foreach (var id in userIds) ctx.Users.Add(MakeUser(id));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SendAsync_Self_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1);

        var result = await svc.SendAsync(1, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinize");
    }

    [Fact]
    public async Task SendAsync_InactiveTarget_ReturnsError()
    {
        var (svc, ctx) = Setup();
        ctx.Users.AddRange(MakeUser(1), MakeUser(2, isActive: false));
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task SendAsync_ValidRequest_CreatesPending()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeTrue();
        result.Connection.Should().NotBeNull();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
        result.Connection.RequesterId.Should().Be(1);
        result.Connection.TargetId.Should().Be(2);
        result.Connection.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ExistingPending_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        await svc.SendAsync(1, 2);

        var second = await svc.SendAsync(1, 2);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("Bekleyen");
    }

    [Fact]
    public async Task SendAsync_ExistingAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);

        var second = await svc.SendAsync(1, 2);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("bağlısınız");
    }

    [Fact]
    public async Task SendAsync_RejectedCooldownActive_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        await svc.RejectAsync(first.Connection!.Id, actingUserId: 2);

        // Yeniden istek — cooldown aktif (RespondedAt henüz birkaç saniye önce)
        var second = await svc.SendAsync(1, 2);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("yakın zamanda");
    }

    [Fact]
    public async Task SendAsync_RejectedCooldownExpired_AllowsResend()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        // Manuel olarak Rejected satırı 31 gün öncesinden ekle
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = 1,
            TargetId = 2,
            Status = UserConnectionStatus.Rejected,
            CreatedAt = DateTime.UtcNow.AddDays(-32),
            RespondedAt = DateTime.UtcNow.AddDays(-31)
        });
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
    }

    [Fact]
    public async Task AcceptAsync_NonTargetUser_ReturnsUnauthorized()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3);
        var first = await svc.SendAsync(1, 2);

        // 3. kullanıcı 1→2 isteğini onaylamaya çalışır
        var result = await svc.AcceptAsync(first.Connection!.Id, actingUserId: 3);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yetk");
    }

    [Fact]
    public async Task AcceptAsync_AlreadyAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);

        var again = await svc.AcceptAsync(first.Connection.Id, actingUserId: 2);

        again.Success.Should().BeFalse();
        again.ErrorMessage.Should().Contain("cevaplanmış");
    }

    [Fact]
    public async Task AcceptAsync_ValidPending_SetsAccepted()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);

        var result = await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Accepted);
        result.Connection.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectAsync_ValidPending_SetsRejectedWithRespondedAt()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);

        var result = await svc.RejectAsync(first.Connection!.Id, actingUserId: 2);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Rejected);
        result.Connection.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAsync_NonRequester_ReturnsUnauthorized()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);

        // Target (2) cancel edemez — iptal sadece requester için
        var result = await svc.CancelAsync(first.Connection!.Id, actingUserId: 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yalnızca isteği gönderen");
    }

    [Fact]
    public async Task CancelAsync_ValidPending_SetsCancelled()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);

        var result = await svc.CancelAsync(first.Connection!.Id, actingUserId: 1);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Cancelled);
    }

    [Fact]
    public async Task RemoveAsync_NonAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2); // Pending

        var result = await svc.RemoveAsync(first.Connection!.Id, actingUserId: 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("aktif bağlantı");
    }

    [Fact]
    public async Task RemoveAsync_BothPartiesCanRemove_AndHardDeletes()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);

        // Target tarafı kaldırır
        var result = await svc.RemoveAsync(first.Connection.Id, actingUserId: 2);
        result.Success.Should().BeTrue();

        // Hard delete: DB'de satır yok
        var exists = await ctx.UserConnections.AnyAsync(c => c.Id == first.Connection.Id);
        exists.Should().BeFalse("Remove hard delete olmalı (audit log iz tutar)");
    }

    [Fact]
    public async Task GetConnectionState_None_WhenNoRecord()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);

        var state = await svc.GetConnectionStateAsync(1, 2);
        state.State.Should().Be(UserConnectionState.None);
        state.CooldownExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetConnectionState_AllStatesAndPerspectives()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3, 4, 5, 6);

        // 1→2 Pending
        await svc.SendAsync(1, 2);
        (await svc.GetConnectionStateAsync(1, 2)).State.Should().Be(UserConnectionState.PendingSent);
        (await svc.GetConnectionStateAsync(2, 1)).State.Should().Be(UserConnectionState.PendingReceived);

        // 3→4 Accepted
        var s34 = await svc.SendAsync(3, 4);
        await svc.AcceptAsync(s34.Connection!.Id, actingUserId: 4);
        (await svc.GetConnectionStateAsync(3, 4)).State.Should().Be(UserConnectionState.Accepted);
        (await svc.GetConnectionStateAsync(4, 3)).State.Should().Be(UserConnectionState.Accepted);

        // 5→6 Rejected (cooldown aktif — yeni reject)
        var s56 = await svc.SendAsync(5, 6);
        await svc.RejectAsync(s56.Connection!.Id, actingUserId: 6);
        var rejected56 = await svc.GetConnectionStateAsync(5, 6);
        rejected56.State.Should().Be(UserConnectionState.CooldownAfterReject,
            "viewer=requester + cooldown aktif");
        rejected56.CooldownExpiresAt.Should().NotBeNull();
        rejected56.CooldownExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow);
        (await svc.GetConnectionStateAsync(6, 5)).State.Should().Be(UserConnectionState.None,
            "viewer=target → kendi reddi cooldown'a takılmaz");
    }

    [Fact]
    public async Task GetConnectionCount_OnlyAccepted()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3, 4);

        // 1→2 Pending
        await svc.SendAsync(1, 2);
        // 1→3 Accepted
        var s13 = await svc.SendAsync(1, 3);
        await svc.AcceptAsync(s13.Connection!.Id, actingUserId: 3);
        // 4→1 Accepted
        var s41 = await svc.SendAsync(4, 1);
        await svc.AcceptAsync(s41.Connection!.Id, actingUserId: 1);

        var count = await svc.GetConnectionCountAsync(1);
        count.Should().Be(2, "1 → 2 active connections (sadece Accepted)");
    }

    [Fact]
    public async Task GetActiveForUser_ReturnsBothDirections()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3);

        // 1→2 Accepted
        var s12 = await svc.SendAsync(1, 2);
        await svc.AcceptAsync(s12.Connection!.Id, actingUserId: 2);
        // 3→1 Accepted
        var s31 = await svc.SendAsync(3, 1);
        await svc.AcceptAsync(s31.Connection!.Id, actingUserId: 1);

        var list = await svc.GetActiveForUserAsync(1);
        list.Should().HaveCount(2);
        list.Should().Contain(c => c.RequesterId == 1 && c.TargetId == 2);
        list.Should().Contain(c => c.RequesterId == 3 && c.TargetId == 1);
    }

    [Fact]
    public async Task GetPendingVsSent_Perspectives()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2, 3);

        // 1→2 Pending (1 sent, 2 pending received)
        await svc.SendAsync(1, 2);
        // 3→1 Pending (3 sent, 1 pending received)
        await svc.SendAsync(3, 1);

        var pendingForUser1 = await svc.GetPendingForUserAsync(1);
        pendingForUser1.Should().HaveCount(1);
        pendingForUser1[0].RequesterId.Should().Be(3);

        var sentByUser1 = await svc.GetSentByUserAsync(1);
        sentByUser1.Should().HaveCount(1);
        sentByUser1[0].TargetId.Should().Be(2);
    }

    // ─── Faz 4.2 P3b/3 — Notification entegrasyon testleri ────────────────

    [Fact]
    public async Task SendAsync_CreatesConnectionRequestNotificationForTarget()
    {
        var (svc, ctx, notif) = SetupRecording();
        await SeedUsersAsync(ctx, 1, 2);
        ctx.UserProfiles.Add(new LexCalculus.Core.Entities.Identity.UserProfile
        {
            UserId = 1, DisplayName = "Mesut Avukat"
        });
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeTrue();
        notif.Created.Should().HaveCount(1);
        var n = notif.Created[0];
        n.Type.Should().Be(LexCalculus.Core.Notifications.NotificationType.ConnectionRequest);
        n.UserId.Should().Be(2, "bildirim hedef kullanıcıya gider");
        n.Body.Should().Contain("Mesut Avukat");
        n.Link.Should().Be("/baglantilarim?tab=bekleyen");
        n.RelatedEntityType.Should().Be(nameof(UserConnection));
        n.RelatedEntityId.Should().Be(result.Connection!.Id);
    }

    [Fact]
    public async Task AcceptAsync_CreatesConnectionAcceptedNotificationForRequester()
    {
        var (svc, ctx, notif) = SetupRecording();
        await SeedUsersAsync(ctx, 1, 2);
        ctx.UserProfiles.Add(new LexCalculus.Core.Entities.Identity.UserProfile
        {
            UserId = 2, DisplayName = "Hâkim Ali"
        });
        await ctx.SaveChangesAsync();

        var first = await svc.SendAsync(1, 2);
        notif.Created.Clear(); // sadece accept'i izle

        var accept = await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);

        accept.Success.Should().BeTrue();
        notif.Created.Should().HaveCount(1);
        var n = notif.Created[0];
        n.Type.Should().Be(LexCalculus.Core.Notifications.NotificationType.ConnectionAccepted);
        n.UserId.Should().Be(1, "bildirim isteği gönderene gider");
        n.Body.Should().Contain("Hâkim Ali");
        n.RelatedEntityId.Should().Be(first.Connection.Id);
    }

    [Fact]
    public async Task RejectAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        notif.Created.Clear();

        await svc.RejectAsync(first.Connection!.Id, actingUserId: 2);

        notif.Created.Should().BeEmpty("LinkedIn pattern: reddedildi sessiz");
    }

    [Fact]
    public async Task CancelAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        notif.Created.Clear();

        await svc.CancelAsync(first.Connection!.Id, actingUserId: 1);

        notif.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        await SeedUsersAsync(ctx, 1, 2);
        var first = await svc.SendAsync(1, 2);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: 2);
        notif.Created.Clear();

        await svc.RemoveAsync(first.Connection.Id, actingUserId: 1);

        notif.Created.Should().BeEmpty("LinkedIn pattern: bağlantı kaldırma sessiz");
    }

    [Fact]
    public async Task SendAsync_NotificationFails_DoesNotBreakSendOperation()
    {
        // Notification fail durumunda asıl Pending kayıt yine de oluşur (defansif).
        var ctx = TestDbContextFactory.Create();
        var failing = new ThrowingNotificationService();
        var svc = new ConnectionService(ctx, new NullActivityLogService(), failing,
            new UserBlockService(ctx, new NullActivityLogService()));
        await SeedUsersAsync(ctx, 1, 2);

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeTrue("notification fail asıl işlemi bozmamalı");
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
    }

    [Fact]
    public async Task SendAsync_BlockerToBlocked_ReturnsGenericError()
    {
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        await blockSvc.BlockAsync(1, 2);

        var result = await svc.SendAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Bu kullanıcıyla bağlantı kuramazsınız.",
            "sessiz pattern: 'engelleme' deme");
    }

    [Fact]
    public async Task SendAsync_BlockedToBlocker_ReturnsGenericError()
    {
        // Karşı yön: 2, 1 tarafından engellenmiş; 2 → 1 bağlantı isteği denerse de
        // engellenir (mutual check).
        var (svc, ctx) = Setup();
        await SeedUsersAsync(ctx, 1, 2);
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        await blockSvc.BlockAsync(1, 2);

        var result = await svc.SendAsync(2, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Bu kullanıcıyla bağlantı kuramazsınız.");
    }

    private sealed class ThrowingNotificationService : LexCalculus.Core.Notifications.INotificationService
    {
        public Task<LexCalculus.Core.Entities.Notifications.Notification?> CreateAsync(
            LexCalculus.Core.Notifications.NotificationType type, int userId,
            string title, string body, string? link = null, string? relatedEntityType = null,
            int? relatedEntityId = null, string? iconHint = null,
            TimeSpan? dedupWindow = null, CancellationToken ct = default)
            => throw new InvalidOperationException("test: notification servis düştü");

        public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LexCalculus.Core.Entities.Notifications.Notification>> GetForUserAsync(
            int userId, int limit, bool unreadOnly, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LexCalculus.Core.Entities.Notifications.Notification>>(
                Array.Empty<LexCalculus.Core.Entities.Notifications.Notification>());
        public Task MarkAsReadAsync(int notificationId, int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> MarkAllAsReadAsync(int userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetTotalActiveCountAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
