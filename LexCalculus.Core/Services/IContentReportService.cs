using LexCalculus.Core.Entities.Moderation;

namespace LexCalculus.Core.Services;

/// <summary>
/// İçerik raporlama servisi (kullanıcı şikayet + admin moderasyon iskeleti).
/// CreateAsync kullanıcı UI'dan çağrılır; DismissAsync/ActionAsync admin UI'dan
/// (P2'de). Polimorfik (TargetType + TargetId), gerçek FK yok — varlık doğrulaması
/// servis tarafında yapılır. Faz 4.10 P1, charter §3.3.
/// </summary>
public interface IContentReportService
{
    /// <summary>
    /// Yeni şikayet oluştur. Validate: hedef varlık var ve yayında, self-report
    /// engeli, mükerrer engel, Reason=Other ise Note (min 10 char) zorunlu.
    /// </summary>
    Task<ContentReportResult> CreateAsync(
        ContentReportTargetType targetType,
        int targetId,
        int reporterId,
        ContentReportReason reason,
        string? note,
        CancellationToken ct = default);

    /// <summary>Admin index için: pending şikayetleri target başına gruplandırır.</summary>
    Task<IReadOnlyList<ContentReportGroup>> GetPendingGroupedAsync(CancellationToken ct = default);

    Task<ContentReport?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Belirli hedef için raporlar (admin detay view).</summary>
    Task<IReadOnlyList<ContentReport>> GetByTargetAsync(
        ContentReportTargetType targetType, int targetId, CancellationToken ct = default);

    /// <summary>Admin reddi: hedef için tüm Pending raporları Dismissed yap. P2'de UI.</summary>
    Task<ContentReportResult> DismissAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, string? reviewNote, CancellationToken ct = default);

    /// <summary>Admin aksiyonu: içeriği sil + raporları Actioned yap. P2'de UI.</summary>
    Task<ContentReportResult> ActionAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, string? reviewNote, CancellationToken ct = default);

    /// <summary>UI helper: kullanıcı bu hedefi zaten şikayet etti mi?</summary>
    Task<bool> HasReportedAsync(
        ContentReportTargetType targetType, int targetId, int userId, CancellationToken ct = default);

    /// <summary>Admin sidebar badge için: bekleyen rapor satır sayısı.</summary>
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}

public sealed record ContentReportResult(bool Success, string? ErrorMessage, ContentReport? Report);

/// <summary>
/// Admin index gruplandırması: hedef başına rapor sayısı + son rapor zamanı +
/// hedefin başlık/preview ve sahibi. TargetTitle: post için Title, comment için
/// body kısaltılmış preview.
/// </summary>
public sealed record ContentReportGroup(
    ContentReportTargetType TargetType,
    int TargetId,
    int ReportCount,
    DateTime LatestReportAt,
    string? TargetTitle,
    string? AuthorDisplayName);
