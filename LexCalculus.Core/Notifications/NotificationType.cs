namespace LexCalculus.Core.Notifications;

/// <summary>
/// Bildirim tipi. Explicit int değerleri kullanılır — yeni tip eklendiğinde
/// mevcut DB satırlarındaki int değerler değişmesin.
/// </summary>
public enum NotificationType
{
    /// <summary>Bir parametre tazelik tolerance'ını aştı (DataFreshnessCheckJob).</summary>
    DataFreshness = 1,

    /// <summary>Admin bir parametreyi güncelledi/yeni versiyon ekledi (kullanıcılara info).</summary>
    ParameterChange = 2,

    /// <summary>Sistem geneli uyarı: bakım, kapanma, kritik duyuru.</summary>
    SystemAlert = 3,

    // ─── Faz 4 (sosyal platform) — şimdiden int rezerve ───

    /// <summary>Faz 4 — bir kullanıcıdan bağlantı isteği geldi.</summary>
    ConnectionRequest = 100,

    /// <summary>Faz 4 — yeni mesaj alındı.</summary>
    NewMessage = 101,

    /// <summary>Faz 4 — bir post'a yorum yapıldı.</summary>
    PostComment = 102
}
