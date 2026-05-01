using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class ConnectionService : IConnectionService
{
    /// <summary>
    /// Reddedildikten sonra aynı yöne tekrar istek atabilmek için bekleme süresi.
    /// Charter §3.2 Karar 2 (spam koruması). Faz 5+'ta config'e taşınabilir.
    /// </summary>
    private const int RejectionCooldownDays = 30;

    private readonly ApplicationDbContext _ctx;
    private readonly IActivityLogService _activityLog;

    public ConnectionService(ApplicationDbContext ctx, IActivityLogService activityLog)
    {
        _ctx = ctx;
        _activityLog = activityLog;
    }

    public async Task<ConnectionResult> SendAsync(
        int requesterId, int targetId, CancellationToken ct = default)
    {
        if (requesterId == targetId)
            return new ConnectionResult(false, "Kendinize bağlantı isteği gönderemezsiniz.", null);

        var target = await _ctx.Users.AsAdminQuery()
            .Where(u => u.Id == targetId)
            .Select(u => new { u.Id, u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (target is null || !target.IsActive)
            return new ConnectionResult(false, "Hedef kullanıcı bulunamadı.", null);

        // Mevcut Pending veya Accepted (her iki yönde) — engelleyici durumlar
        var blocking = await _ctx.UserConnections
            .Where(c => ((c.RequesterId == requesterId && c.TargetId == targetId)
                      || (c.RequesterId == targetId && c.TargetId == requesterId))
                     && (c.Status == UserConnectionStatus.Pending
                      || c.Status == UserConnectionStatus.Accepted))
            .FirstOrDefaultAsync(ct);

        if (blocking is not null)
        {
            return blocking.Status == UserConnectionStatus.Accepted
                ? new ConnectionResult(false, "Zaten bağlısınız.", null)
                : new ConnectionResult(false, "Bekleyen bir bağlantı isteği zaten var.", null);
        }

        // Cooldown — bu kullanıcıdan o kullanıcıya gönderilen son Rejected.
        // (Karşı yönden Rejected requester'ı engellemez — yine de istek atabilir.)
        var cooldownThreshold = DateTime.UtcNow.AddDays(-RejectionCooldownDays);
        var recentReject = await _ctx.UserConnections
            .Where(c => c.RequesterId == requesterId
                     && c.TargetId == targetId
                     && c.Status == UserConnectionStatus.Rejected
                     && c.RespondedAt != null
                     && c.RespondedAt > cooldownThreshold)
            .AnyAsync(ct);

        if (recentReject)
            return new ConnectionResult(false,
                $"Bu kullanıcıya yakın zamanda istek gönderdiniz. {RejectionCooldownDays} gün sonra tekrar deneyebilirsiniz.",
                null);

        var conn = new UserConnection
        {
            RequesterId = requesterId,
            TargetId = targetId,
            Status = UserConnectionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.UserConnections.Add(conn);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserConnection.Send",
            entityType: nameof(UserConnection),
            entityId: conn.Id,
            description: $"Bağlantı isteği gönderildi: {requesterId} → {targetId}",
            metadata: new { RequesterId = requesterId, TargetId = targetId },
            ct: ct);

        return new ConnectionResult(true, null, conn);
    }

    public async Task<ConnectionResult> AcceptAsync(
        int connectionId, int actingUserId, CancellationToken ct = default)
    {
        var conn = await _ctx.UserConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (conn is null)
            return new ConnectionResult(false, "Bağlantı bulunamadı.", null);
        if (conn.TargetId != actingUserId)
            return new ConnectionResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);
        if (conn.Status != UserConnectionStatus.Pending)
            return new ConnectionResult(false, "Bu istek zaten cevaplanmış.", null);

        conn.Status = UserConnectionStatus.Accepted;
        conn.RespondedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserConnection.Accept",
            entityType: nameof(UserConnection),
            entityId: conn.Id,
            description: $"Bağlantı isteği kabul edildi: {conn.RequesterId} → {conn.TargetId}",
            metadata: new { RequesterId = conn.RequesterId, TargetId = conn.TargetId },
            ct: ct);

        return new ConnectionResult(true, null, conn);
    }

    public async Task<ConnectionResult> RejectAsync(
        int connectionId, int actingUserId, CancellationToken ct = default)
    {
        var conn = await _ctx.UserConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (conn is null)
            return new ConnectionResult(false, "Bağlantı bulunamadı.", null);
        if (conn.TargetId != actingUserId)
            return new ConnectionResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);
        if (conn.Status != UserConnectionStatus.Pending)
            return new ConnectionResult(false, "Bu istek zaten cevaplanmış.", null);

        conn.Status = UserConnectionStatus.Rejected;
        conn.RespondedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserConnection.Reject",
            entityType: nameof(UserConnection),
            entityId: conn.Id,
            description: $"Bağlantı isteği reddedildi: {conn.RequesterId} → {conn.TargetId}",
            metadata: new { RequesterId = conn.RequesterId, TargetId = conn.TargetId },
            ct: ct);

        return new ConnectionResult(true, null, conn);
    }

    public async Task<ConnectionResult> CancelAsync(
        int connectionId, int actingUserId, CancellationToken ct = default)
    {
        var conn = await _ctx.UserConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (conn is null)
            return new ConnectionResult(false, "Bağlantı bulunamadı.", null);
        if (conn.RequesterId != actingUserId)
            return new ConnectionResult(false, "Yalnızca isteği gönderen iptal edebilir.", null);
        if (conn.Status != UserConnectionStatus.Pending)
            return new ConnectionResult(false, "Yalnızca bekleyen istek iptal edilebilir.", null);

        conn.Status = UserConnectionStatus.Cancelled;
        conn.RespondedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserConnection.Cancel",
            entityType: nameof(UserConnection),
            entityId: conn.Id,
            description: $"Bağlantı isteği iptal edildi: {conn.RequesterId} → {conn.TargetId}",
            metadata: new { RequesterId = conn.RequesterId, TargetId = conn.TargetId },
            ct: ct);

        return new ConnectionResult(true, null, conn);
    }

    public async Task<ConnectionResult> RemoveAsync(
        int connectionId, int actingUserId, CancellationToken ct = default)
    {
        var conn = await _ctx.UserConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (conn is null)
            return new ConnectionResult(false, "Bağlantı bulunamadı.", null);
        if (conn.RequesterId != actingUserId && conn.TargetId != actingUserId)
            return new ConnectionResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);
        if (conn.Status != UserConnectionStatus.Accepted)
            return new ConnectionResult(false, "Yalnızca aktif bağlantı kaldırılabilir.", null);

        // Hard delete — audit için ID + perspektif yerel değişkenlerde tutulur.
        var connId = conn.Id;
        var requesterId = conn.RequesterId;
        var targetId = conn.TargetId;

        _ctx.UserConnections.Remove(conn);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "UserConnection.Remove",
            entityType: nameof(UserConnection),
            entityId: connId,
            description: $"Bağlantı kaldırıldı (hard delete): {requesterId} ↔ {targetId} (eylem: {actingUserId})",
            metadata: new { RequesterId = requesterId, TargetId = targetId, ActingUserId = actingUserId },
            ct: ct);

        return new ConnectionResult(true, null, null);
    }

    public async Task<ConnectionStateResult> GetConnectionStateAsync(
        int viewerUserId, int targetUserId, CancellationToken ct = default)
    {
        if (viewerUserId == targetUserId)
            return new ConnectionStateResult(UserConnectionState.None);

        // En son ilgili kayıt (her iki yön)
        var existing = await _ctx.UserConnections
            .Where(c => (c.RequesterId == viewerUserId && c.TargetId == targetUserId)
                     || (c.RequesterId == targetUserId && c.TargetId == viewerUserId))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null) return new ConnectionStateResult(UserConnectionState.None);

        switch (existing.Status)
        {
            case UserConnectionStatus.Accepted:
                return new ConnectionStateResult(UserConnectionState.Accepted);

            case UserConnectionStatus.Pending:
                return new ConnectionStateResult(
                    existing.RequesterId == viewerUserId
                        ? UserConnectionState.PendingSent
                        : UserConnectionState.PendingReceived);

            case UserConnectionStatus.Rejected:
                // Cooldown sadece viewer→target yönündeki Rejected için
                if (existing.RequesterId == viewerUserId && existing.RespondedAt.HasValue)
                {
                    var expiresAt = existing.RespondedAt.Value.AddDays(RejectionCooldownDays);
                    if (expiresAt > DateTime.UtcNow)
                        return new ConnectionStateResult(
                            UserConnectionState.CooldownAfterReject, expiresAt);
                }
                return new ConnectionStateResult(UserConnectionState.None);

            case UserConnectionStatus.Cancelled:
            default:
                return new ConnectionStateResult(UserConnectionState.None);
        }
    }

    public Task<int> GetConnectionCountAsync(int userId, CancellationToken ct = default)
        => _ctx.UserConnections
            .CountAsync(c =>
                (c.RequesterId == userId || c.TargetId == userId)
                && c.Status == UserConnectionStatus.Accepted, ct);

    public async Task<IReadOnlyList<UserConnection>> GetActiveForUserAsync(
        int userId, CancellationToken ct = default)
    {
        return await _ctx.UserConnections
            .Include(c => c.Requester).ThenInclude(u => u!.Profile)
            .Include(c => c.Target).ThenInclude(u => u!.Profile)
            .Where(c => (c.RequesterId == userId || c.TargetId == userId)
                     && c.Status == UserConnectionStatus.Accepted)
            .OrderByDescending(c => c.RespondedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserConnection>> GetPendingForUserAsync(
        int userId, CancellationToken ct = default)
    {
        return await _ctx.UserConnections
            .Include(c => c.Requester).ThenInclude(u => u!.Profile)
            .Where(c => c.TargetId == userId && c.Status == UserConnectionStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserConnection>> GetSentByUserAsync(
        int userId, CancellationToken ct = default)
    {
        return await _ctx.UserConnections
            .Include(c => c.Target).ThenInclude(u => u!.Profile)
            .Where(c => c.RequesterId == userId && c.Status == UserConnectionStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }
}
