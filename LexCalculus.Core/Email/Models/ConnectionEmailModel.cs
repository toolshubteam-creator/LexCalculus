namespace LexCalculus.Core.Email.Models;

/// <summary>
/// Bağlantı isteği / kabul bildirimi e-posta modeli (#22, sosyal bildirim).
/// IsAccepted=false: yeni istek geldi; true: gönderilen istek kabul edildi.
/// </summary>
public sealed class ConnectionEmailModel
{
    public required string RecipientDisplayName { get; init; }
    public required string OtherDisplayName { get; init; }
    public required bool IsAccepted { get; init; }
    public required string ProfileUrl { get; init; }
}
