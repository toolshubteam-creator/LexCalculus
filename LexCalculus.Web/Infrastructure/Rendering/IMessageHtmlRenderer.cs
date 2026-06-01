using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Web.Models;

namespace LexCalculus.Web.Infrastructure.Rendering;

/// <summary>
/// Bir <see cref="Message"/> entity'sini belirli bir viewer perspektifinden
/// <see cref="MessageViewModel"/>'e kurar ve <c>_Message</c> partial'ı HTML'e
/// render eder. Faz 6.11 (#17/#38) — önceden <c>MessagesController.BuildVm</c>
/// ve <c>SignalRMessagingNotifier</c> içinde neredeyse birebir tekrarlanan
/// "VM kur (avatar URL, displayName anonimize fallback, IsOwnMessage perspektifi)
/// + render" dizisi tek noktaya toplandı. <see cref="IPartialRenderer"/>
/// primitive'i üzerine kompozisyon (o zaten paylaşılıyordu).
/// </summary>
public interface IMessageHtmlRenderer
{
    /// <summary>
    /// Mesajı viewer perspektifinde VM'e map'ler. <paramref name="message"/>
    /// <c>Sender</c> + <c>Sender.Profile</c> include edilmiş olmalı.
    /// IsOwnMessage = (SenderId == viewerId).
    /// </summary>
    MessageViewModel BuildViewModel(Message message, int viewerId);

    /// <summary>
    /// <see cref="BuildViewModel"/> + <c>_Message</c> partial render → HTML string.
    /// </summary>
    Task<string> RenderForViewerAsync(Message message, int viewerId, CancellationToken ct = default);
}
