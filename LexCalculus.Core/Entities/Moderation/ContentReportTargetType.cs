namespace LexCalculus.Core.Entities.Moderation;

/// <summary>
/// İçerik raporlamanın hedef tipi. ContentReport tablosu polimorfik
/// (TargetType + TargetId) — gerçek FK yok, varlık doğrulaması servis
/// tarafında yapılır. Faz 4.10 P1.
/// </summary>
public enum ContentReportTargetType
{
    Post = 1,
    Comment = 2
}
