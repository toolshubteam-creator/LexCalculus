namespace LexCalculus.Core.Entities.Moderation;

/// <summary>
/// İçerik raporlamanın hedef tipi. ContentReport tablosu polimorfik
/// (TargetType + TargetId) — gerçek FK yok, varlık doğrulaması servis
/// tarafında yapılır. Faz 4.10 P1.
/// </summary>
public enum ContentReportTargetType
{
    Post = 1,
    Comment = 2,

    /// <summary>
    /// Faz 5.7 — 1-1 doğrudan mesaj. ContentReportService.CreateAsync için yetki
    /// kontrolü konuşma katılımcılığı (rastgele mesaj id ile spam engel).
    /// Hide → IsModeratorHidden=true (alıcı için filter, sahip için placeholder).
    /// </summary>
    Message = 3
}
