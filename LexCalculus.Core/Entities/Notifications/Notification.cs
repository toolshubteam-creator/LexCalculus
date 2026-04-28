using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Notifications;

namespace LexCalculus.Core.Entities.Notifications;

/// <summary>
/// Kullanıcıya gösterilecek bildirim. Bell icon dropdown ve /bildirimler
/// sayfasında listelenir. Hangfire job'ları (DataFreshnessCheckJob gibi)
/// veya admin (SystemAlert) tarafından oluşturulur.
/// </summary>
public sealed class Notification
{
    public int Id { get; set; }

    /// <summary>Bildirim alıcısı (ApplicationUser FK, cascade delete).</summary>
    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>Kısa başlık, örn. "Asgari ücret parametresi güncel değil".</summary>
    public string Title { get; set; } = "";

    /// <summary>Detaylı gövde, örn. "Asgari ücret 6 ay önce kontrol edildi...".</summary>
    public string Body { get; set; } = "";

    /// <summary>Tıklanabilir hedef URL (göreli yol). null ise sadece bilgi.</summary>
    public string? Link { get; set; }

    /// <summary>UI ipucu — Parça 6/6'da farklı ikonlar için. null ise tipten türetilir.</summary>
    public string? IconHint { get; set; }

    /// <summary>Dedup için: bildirimle ilişkili entity tipi, örn. "FormulaParameter".</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>Dedup için: ilişkili entity ID'si.</summary>
    public int? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
