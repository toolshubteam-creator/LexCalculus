namespace LexCalculus.Core.Entities.Moderation;

/// <summary>
/// Şikayetin yaşam döngüsü. Pending: admin incelemesi bekliyor.
/// Dismissed: admin reddetti, içerik kalır. Actioned: admin sildi,
/// içerik kaldırıldı. Faz 4.10 P1.
/// </summary>
public enum ContentReportStatus
{
    Pending = 0,
    Dismissed = 1,
    Actioned = 2
}
