namespace LexCalculus.Core.Entities.Moderation;

/// <summary>
/// Şikayet sebep kategorileri. Other (99) seçildiğinde Note alanı zorunlu
/// (servis-side validation). Faz 4.10 P1.
/// </summary>
public enum ContentReportReason
{
    Spam = 1,
    Harassment = 2,
    Misleading = 3,
    Legal = 4,
    Obscene = 5,
    Other = 99
}
