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

public class ConnectionServiceTests : SqlServerTestBase
{
    private (ConnectionService svc, ApplicationDbContext ctx) Setup()
    {
        var ctx = _db.Create();
        var svc = new ConnectionService(ctx, new NullActivityLogService(),
            new NullNotificationService(),
            new UserBlockService(ctx, new NullActivityLogService()));
        return (svc, ctx);
    }

    private (ConnectionService svc, ApplicationDbContext ctx, RecordingNotificationService notif) SetupRecording()
    {
        var ctx = _db.Create();
        var notif = new RecordingNotificationService();
        var svc = new ConnectionService(ctx, new NullActivityLogService(), notif,
            new UserBlockService(ctx, new NullActivityLogService()));
        return (svc, ctx, notif);
    }

    private static ApplicationUser MakeUser(string suffix, bool isActive = true) => new()
    {
        UserName = $"u{suffix}@x.com",
        NormalizedUserName = $"U{suffix}@X.COM",
        Email = $"u{suffix}@x.com",
        NormalizedEmail = $"U{suffix}@X.COM",
        FullName = $"User {suffix}",
        CreatedAt = DateTime.UtcNow,
        IsActive = isActive,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    /// <summary>
    /// Seeds N users with suffixes "1", "2", ... and returns their generated Ids in order.
    /// </summary>
    private static async Task<int[]> SeedUsersAsync(ApplicationDbContext ctx, int count)
    {
        var users = new ApplicationUser[count];
        for (int i = 0; i < count; i++)
        {
            users[i] = MakeUser((i + 1).ToString());
            ctx.Users.Add(users[i]);
        }
        await ctx.SaveChangesAsync();
        return users.Select(u => u.Id).ToArray();
    }

    [Fact]
    public async Task SendAsync_Self_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 1);

        var result = await svc.SendAsync(ids[0], ids[0]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Kendinize");
    }

    [Fact]
    public async Task SendAsync_InactiveTarget_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var u1 = MakeUser("1");
        var u2 = MakeUser("2", isActive: false);
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(u1.Id, u2.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bulunamadı");
    }

    [Fact]
    public async Task SendAsync_ValidRequest_CreatesPending()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);

        var result = await svc.SendAsync(ids[0], ids[1]);

        result.Success.Should().BeTrue();
        result.Connection.Should().NotBeNull();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
        result.Connection.RequesterId.Should().Be(ids[0]);
        result.Connection.TargetId.Should().Be(ids[1]);
        result.Connection.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ExistingPending_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        await svc.SendAsync(ids[0], ids[1]);

        var second = await svc.SendAsync(ids[0], ids[1]);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("Bekleyen");
    }

    [Fact]
    public async Task SendAsync_ExistingAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);

        var second = await svc.SendAsync(ids[0], ids[1]);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("bağlısınız");
    }

    [Fact]
    public async Task SendAsync_RejectedCooldownActive_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        await svc.RejectAsync(first.Connection!.Id, actingUserId: ids[1]);

        // Yeniden istek — cooldown aktif (RespondedAt henüz birkaç saniye önce)
        var second = await svc.SendAsync(ids[0], ids[1]);

        second.Success.Should().BeFalse();
        second.ErrorMessage.Should().Contain("yakın zamanda");
    }

    [Fact]
    public async Task SendAsync_RejectedCooldownExpired_AllowsResend()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);

        // Manuel olarak Rejected satırı 31 gün öncesinden ekle
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = ids[0],
            TargetId = ids[1],
            Status = UserConnectionStatus.Rejected,
            CreatedAt = DateTime.UtcNow.AddDays(-32),
            RespondedAt = DateTime.UtcNow.AddDays(-31)
        });
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(ids[0], ids[1]);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
    }

    [Fact]
    public async Task AcceptAsync_NonTargetUser_ReturnsUnauthorized()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 3);
        var first = await svc.SendAsync(ids[0], ids[1]);

        // 3. kullanıcı 1→2 isteğini onaylamaya çalışır
        var result = await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[2]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yetk");
    }

    [Fact]
    public async Task AcceptAsync_AlreadyAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);

        var again = await svc.AcceptAsync(first.Connection.Id, actingUserId: ids[1]);

        again.Success.Should().BeFalse();
        again.ErrorMessage.Should().Contain("cevaplanmış");
    }

    [Fact]
    public async Task AcceptAsync_ValidPending_SetsAccepted()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);

        var result = await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Accepted);
        result.Connection.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectAsync_ValidPending_SetsRejectedWithRespondedAt()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);

        var result = await svc.RejectAsync(first.Connection!.Id, actingUserId: ids[1]);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Rejected);
        result.Connection.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelAsync_NonRequester_ReturnsUnauthorized()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);

        // Target (2) cancel edemez — iptal sadece requester için
        var result = await svc.CancelAsync(first.Connection!.Id, actingUserId: ids[1]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Yalnızca isteği gönderen");
    }

    [Fact]
    public async Task CancelAsync_ValidPending_SetsCancelled()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);

        var result = await svc.CancelAsync(first.Connection!.Id, actingUserId: ids[0]);

        result.Success.Should().BeTrue();
        result.Connection!.Status.Should().Be(UserConnectionStatus.Cancelled);
    }

    [Fact]
    public async Task RemoveAsync_NonAccepted_ReturnsError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]); // Pending

        var result = await svc.RemoveAsync(first.Connection!.Id, actingUserId: ids[0]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("aktif bağlantı");
    }

    [Fact]
    public async Task RemoveAsync_BothPartiesCanRemove_AndHardDeletes()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);

        // Target tarafı kaldırır
        var result = await svc.RemoveAsync(first.Connection.Id, actingUserId: ids[1]);
        result.Success.Should().BeTrue();

        // Hard delete: DB'de satır yok
        var exists = await ctx.UserConnections.AnyAsync(c => c.Id == first.Connection.Id);
        exists.Should().BeFalse("Remove hard delete olmalı (audit log iz tutar)");
    }

    [Fact]
    public async Task GetConnectionState_None_WhenNoRecord()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);

        var state = await svc.GetConnectionStateAsync(ids[0], ids[1]);
        state.State.Should().Be(UserConnectionState.None);
        state.CooldownExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task GetConnectionState_AllStatesAndPerspectives()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 6);

        // 1→2 Pending
        await svc.SendAsync(ids[0], ids[1]);
        (await svc.GetConnectionStateAsync(ids[0], ids[1])).State.Should().Be(UserConnectionState.PendingSent);
        (await svc.GetConnectionStateAsync(ids[1], ids[0])).State.Should().Be(UserConnectionState.PendingReceived);

        // 3→4 Accepted
        var s34 = await svc.SendAsync(ids[2], ids[3]);
        await svc.AcceptAsync(s34.Connection!.Id, actingUserId: ids[3]);
        (await svc.GetConnectionStateAsync(ids[2], ids[3])).State.Should().Be(UserConnectionState.Accepted);
        (await svc.GetConnectionStateAsync(ids[3], ids[2])).State.Should().Be(UserConnectionState.Accepted);

        // 5→6 Rejected (cooldown aktif — yeni reject)
        var s56 = await svc.SendAsync(ids[4], ids[5]);
        await svc.RejectAsync(s56.Connection!.Id, actingUserId: ids[5]);
        var rejected56 = await svc.GetConnectionStateAsync(ids[4], ids[5]);
        rejected56.State.Should().Be(UserConnectionState.CooldownAfterReject,
            "viewer=requester + cooldown aktif");
        rejected56.CooldownExpiresAt.Should().NotBeNull();
        rejected56.CooldownExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow);
        (await svc.GetConnectionStateAsync(ids[5], ids[4])).State.Should().Be(UserConnectionState.None,
            "viewer=target → kendi reddi cooldown'a takılmaz");
    }

    [Fact]
    public async Task GetConnectionCount_OnlyAccepted()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 4);

        // 1→2 Pending
        await svc.SendAsync(ids[0], ids[1]);
        // 1→3 Accepted
        var s13 = await svc.SendAsync(ids[0], ids[2]);
        await svc.AcceptAsync(s13.Connection!.Id, actingUserId: ids[2]);
        // 4→1 Accepted
        var s41 = await svc.SendAsync(ids[3], ids[0]);
        await svc.AcceptAsync(s41.Connection!.Id, actingUserId: ids[0]);

        var count = await svc.GetConnectionCountAsync(ids[0]);
        count.Should().Be(2, "1 → 2 active connections (sadece Accepted)");
    }

    [Fact]
    public async Task GetActiveForUser_ReturnsBothDirections()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 3);

        // 1→2 Accepted
        var s12 = await svc.SendAsync(ids[0], ids[1]);
        await svc.AcceptAsync(s12.Connection!.Id, actingUserId: ids[1]);
        // 3→1 Accepted
        var s31 = await svc.SendAsync(ids[2], ids[0]);
        await svc.AcceptAsync(s31.Connection!.Id, actingUserId: ids[0]);

        var list = await svc.GetActiveForUserAsync(ids[0]);
        list.Should().HaveCount(2);
        list.Should().Contain(c => c.RequesterId == ids[0] && c.TargetId == ids[1]);
        list.Should().Contain(c => c.RequesterId == ids[2] && c.TargetId == ids[0]);
    }

    [Fact]
    public async Task GetPendingVsSent_Perspectives()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 3);

        // 1→2 Pending (1 sent, 2 pending received)
        await svc.SendAsync(ids[0], ids[1]);
        // 3→1 Pending (3 sent, 1 pending received)
        await svc.SendAsync(ids[2], ids[0]);

        var pendingForUser1 = await svc.GetPendingForUserAsync(ids[0]);
        pendingForUser1.Should().HaveCount(1);
        pendingForUser1[0].RequesterId.Should().Be(ids[2]);

        var sentByUser1 = await svc.GetSentByUserAsync(ids[0]);
        sentByUser1.Should().HaveCount(1);
        sentByUser1[0].TargetId.Should().Be(ids[1]);
    }

    // ─── Faz 4.2 P3b/3 — Notification entegrasyon testleri ────────────────

    [Fact]
    public async Task SendAsync_CreatesConnectionRequestNotificationForTarget()
    {
        var (svc, ctx, notif) = SetupRecording();
        var ids = await SeedUsersAsync(ctx, 2);
        ctx.UserProfiles.Add(new LexCalculus.Core.Entities.Identity.UserProfile
        {
            UserId = ids[0], DisplayName = "Mesut Avukat"
        });
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(ids[0], ids[1]);

        result.Success.Should().BeTrue();
        notif.Created.Should().HaveCount(1);
        var n = notif.Created[0];
        n.Type.Should().Be(LexCalculus.Core.Notifications.NotificationType.ConnectionRequest);
        n.UserId.Should().Be(ids[1], "bildirim hedef kullanıcıya gider");
        n.Body.Should().Contain("Mesut Avukat");
        n.Link.Should().Be("/baglantilarim?tab=bekleyen");
        n.RelatedEntityType.Should().Be(nameof(UserConnection));
        n.RelatedEntityId.Should().Be(result.Connection!.Id);
    }

    [Fact]
    public async Task AcceptAsync_CreatesConnectionAcceptedNotificationForRequester()
    {
        var (svc, ctx, notif) = SetupRecording();
        var ids = await SeedUsersAsync(ctx, 2);
        ctx.UserProfiles.Add(new LexCalculus.Core.Entities.Identity.UserProfile
        {
            UserId = ids[1], DisplayName = "Hâkim Ali"
        });
        await ctx.SaveChangesAsync();

        var first = await svc.SendAsync(ids[0], ids[1]);
        notif.Created.Clear(); // sadece accept'i izle

        var accept = await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);

        accept.Success.Should().BeTrue();
        notif.Created.Should().HaveCount(1);
        var n = notif.Created[0];
        n.Type.Should().Be(LexCalculus.Core.Notifications.NotificationType.ConnectionAccepted);
        n.UserId.Should().Be(ids[0], "bildirim isteği gönderene gider");
        n.Body.Should().Contain("Hâkim Ali");
        n.RelatedEntityId.Should().Be(first.Connection.Id);
    }

    [Fact]
    public async Task RejectAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        notif.Created.Clear();

        await svc.RejectAsync(first.Connection!.Id, actingUserId: ids[1]);

        notif.Created.Should().BeEmpty("LinkedIn pattern: reddedildi sessiz");
    }

    [Fact]
    public async Task CancelAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        notif.Created.Clear();

        await svc.CancelAsync(first.Connection!.Id, actingUserId: ids[0]);

        notif.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_DoesNotCreateNotification()
    {
        var (svc, ctx, notif) = SetupRecording();
        var ids = await SeedUsersAsync(ctx, 2);
        var first = await svc.SendAsync(ids[0], ids[1]);
        await svc.AcceptAsync(first.Connection!.Id, actingUserId: ids[1]);
        notif.Created.Clear();

        await svc.RemoveAsync(first.Connection.Id, actingUserId: ids[0]);

        notif.Created.Should().BeEmpty("LinkedIn pattern: bağlantı kaldırma sessiz");
    }

    [Fact]
    public async Task SendAsync_NotificationFails_DoesNotBreakSendOperation()
    {
        // Notification fail durumunda asıl Pending kayıt yine de oluşur (defansif).
        var ctx = _db.Create();
        var failing = new ThrowingNotificationService();
        var svc = new ConnectionService(ctx, new NullActivityLogService(), failing,
            new UserBlockService(ctx, new NullActivityLogService()));
        var u1 = MakeUser("1");
        var u2 = MakeUser("2");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var result = await svc.SendAsync(u1.Id, u2.Id);

        result.Success.Should().BeTrue("notification fail asıl işlemi bozmamalı");
        result.Connection!.Status.Should().Be(UserConnectionStatus.Pending);
    }

    [Fact]
    public async Task SendAsync_BlockerToBlocked_ReturnsGenericError()
    {
        var (svc, ctx) = Setup();
        var ids = await SeedUsersAsync(ctx, 2);
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        await blockSvc.BlockAsync(ids[0], ids[1]);

        var result = await svc.SendAsync(ids[0], ids[1]);

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
        var ids = await SeedUsersAsync(ctx, 2);
        var blockSvc = new UserBlockService(ctx, new NullActivityLogService());
        await blockSvc.BlockAsync(ids[0], ids[1]);

        var result = await svc.SendAsync(ids[1], ids[0]);

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
