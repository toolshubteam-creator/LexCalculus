using LexCalculus.Core.Entities.Social;

namespace LexCalculus.Core.Services;

/// <summary>
/// Kullanıcı engelleme servisi (charter §2.1 Karar 5, §3.3). Engelleme tek
/// yönlü tutulur, sessiz pattern: engellenen sessizce fark eder. Block
/// oluşturulurken mevcut Accepted bağlantı varsa otomatik kaldırılır.
/// Notification YOK — Reject/Cancel/Remove gibi sessiz.
/// </summary>
public interface IUserBlockService
{
    Task<UserBlockResult> BlockAsync(int blockerId, int blockedId, CancellationToken ct = default);
    Task<UserBlockResult> UnblockAsync(int blockerId, int blockedId, CancellationToken ct = default);

    /// <summary>blockerId, blockedId'yi engelledi mi.</summary>
    Task<bool> IsBlockedAsync(int blockerId, int blockedId, CancellationToken ct = default);

    /// <summary>İki yönden biri engellenmiş mi (mutual check). ConnectionService kullanır.</summary>
    Task<bool> IsEitherDirectionBlockedAsync(int userA, int userB, CancellationToken ct = default);

    /// <summary>userId'nin engellediği kullanıcıların listesi (Engellenenler sekmesi).</summary>
    Task<IReadOnlyList<UserBlock>> GetBlockedByUserAsync(int userId, CancellationToken ct = default);

    /// <summary>userId'nin engellediği kullanıcı sayısı (sekme badge'i).</summary>
    Task<int> GetBlockedCountAsync(int userId, CancellationToken ct = default);
}

public sealed record UserBlockResult(bool Success, string? ErrorMessage);
