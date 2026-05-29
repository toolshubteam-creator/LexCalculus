namespace LexCalculus.Core.Email.Models;

/// <summary>
/// Okunmamış mesaj dijesti e-posta modeli (#22, sosyal bildirim). P2/2'de
/// Hangfire scheduled job tarafından doldurulup gönderilecek (5 dk pencere).
/// SenderDisplayNames template'te Distinct edilir.
/// </summary>
public sealed class MessageDigestEmailModel
{
    public required string RecipientDisplayName { get; init; }
    public required int UnreadCount { get; init; }
    public required IReadOnlyList<string> SenderDisplayNames { get; init; }
    public required string MessagesUrl { get; init; }
}
