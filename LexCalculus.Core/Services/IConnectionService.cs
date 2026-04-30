using LexCalculus.Core.Entities.Social;

namespace LexCalculus.Core.Services;

/// <summary>
/// LinkedIn-tarzı bağlantı servisi (charter §3.2 Karar 2). State machine:
/// None → Pending → Accepted | Rejected | Cancelled. Remove (Accepted satır
/// için) hard delete.
///
/// Notification entegrasyonu YOK (Adım 4.3 P3'te eklenecek).
/// UserBlock check YOK (Adım 4.3'te eklenecek).
/// </summary>
public interface IConnectionService
{
    Task<ConnectionResult> SendAsync(int requesterId, int targetId, CancellationToken ct = default);
    Task<ConnectionResult> AcceptAsync(int connectionId, int actingUserId, CancellationToken ct = default);
    Task<ConnectionResult> RejectAsync(int connectionId, int actingUserId, CancellationToken ct = default);
    Task<ConnectionResult> CancelAsync(int connectionId, int actingUserId, CancellationToken ct = default);
    Task<ConnectionResult> RemoveAsync(int connectionId, int actingUserId, CancellationToken ct = default);

    Task<UserConnectionState> GetConnectionStateAsync(int viewerUserId, int targetUserId, CancellationToken ct = default);
    Task<int> GetConnectionCountAsync(int userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserConnection>> GetActiveForUserAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserConnection>> GetPendingForUserAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserConnection>> GetSentByUserAsync(int userId, CancellationToken ct = default);
}

public sealed record ConnectionResult(bool Success, string? ErrorMessage, UserConnection? Connection);

public enum UserConnectionState
{
    /// <summary>Hiç bağlantı yok veya cooldown geçmiş — viewer istek atabilir.</summary>
    None,
    /// <summary>Viewer istek gönderdi, target henüz cevaplamadı.</summary>
    PendingSent,
    /// <summary>Viewer'a istek geldi, viewer cevaplamadı.</summary>
    PendingReceived,
    /// <summary>Bağlılar.</summary>
    Accepted,
    /// <summary>Viewer'ın gönderdiği istek reddedildi, 30 gün cooldown aktif.</summary>
    CooldownAfterReject
}
