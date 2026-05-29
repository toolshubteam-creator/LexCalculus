namespace LexCalculus.Core.Email.Models;

/// <summary>
/// Kullanıcının kendi içeriğine moderasyon işlemi uygulandığında gönderilen
/// bildirim e-posta modeli (#22, sosyal bildirim). ActionType ("Gizlendi" /
/// "Kaldırıldı") template'te küçük harfe çevrilerek cümle içinde kullanılır.
/// ContentTitle ve ReviewNote opsiyonel.
/// </summary>
public sealed class ContentReportEmailModel
{
    public required string RecipientDisplayName { get; init; }
    public required string ActionType { get; init; }
    public required string ContentType { get; init; }
    public string? ContentTitle { get; init; }
    public string? ReviewNote { get; init; }
}
