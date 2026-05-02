using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Moderation;

/// <summary>
/// Bir kullanıcının bir içeriği (post veya comment) şikayet etmesi.
/// TargetType + TargetId polimorfik — FK yok, doğrulama servis tarafında.
/// (ReporterId, TargetType, TargetId) UNIQUE → bir kullanıcı bir içeriği
/// yalnızca bir kez şikayet eder. Faz 4.10 P1, charter §3.3 (moderasyon altyapısı).
/// </summary>
public sealed class ContentReport
{
    public int Id { get; set; }

    public ContentReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }

    public int ReporterId { get; set; }
    public ApplicationUser Reporter { get; set; } = null!;

    public ContentReportReason Reason { get; set; }

    /// <summary>500 char max. Reason=Other ise zorunlu (min 10 char, servis kontrolü).</summary>
    public string? Note { get; set; }

    public ContentReportStatus Status { get; set; }

    public int? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Admin moderasyon notu, opsiyonel. 500 char max.</summary>
    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }
}
