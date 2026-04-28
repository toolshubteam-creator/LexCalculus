using LexCalculus.Core.Entities.Notifications;

namespace LexCalculus.Core.Notifications;

/// <summary>
/// Bildirim oluşturma ve okuma servisi. Hangfire job'ları (DataFreshnessCheckJob),
/// admin actionları (parametre değişikliği) ve sistem (SystemAlert) bu servisi
/// kullanır. UI tarafı (Parça 6/6) sadece okuma metotlarını çağırır.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Bir kullanıcıya bildirim oluşturur. Eğer dedupWindow + relatedEntity bilgisi
    /// verilmişse ve aynı pencerede aynı (user, type, relatedEntity) için bildirim
    /// varsa null döner (dedup hit). Aksi halde yeni bildirim oluşturur ve döner.
    /// </summary>
    Task<Notification?> CreateAsync(
        NotificationType type,
        int userId,
        string title,
        string body,
        string? link = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? iconHint = null,
        TimeSpan? dedupWindow = null,
        CancellationToken ct = default);

    /// <summary>Kullanıcının okunmamış bildirim sayısı (bell icon badge).</summary>
    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Kullanıcı için bildirim listesi, en yeniden eskiye sıralı.
    /// limit: maksimum kayıt (bell dropdown 10, /bildirimler sayfası 25 vs).
    /// unreadOnly: true ise sadece okunmamışlar.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId, int limit, bool unreadOnly, CancellationToken ct = default);

    /// <summary>Tek bildirimi okundu işaretler. Yetki kontrolü içerir
    /// (sadece bildirimin sahibi okuyabilir).</summary>
    Task MarkAsReadAsync(int notificationId, int userId, CancellationToken ct = default);

    /// <summary>Kullanıcının tüm okunmamış bildirimlerini okundu işaretler.</summary>
    Task<int> MarkAllAsReadAsync(int userId, CancellationToken ct = default);

    /// <summary>Tüm sistemdeki aktif (silinmemiş) bildirim sayısı (admin widget).</summary>
    Task<int> GetTotalActiveCountAsync(CancellationToken ct = default);
}
