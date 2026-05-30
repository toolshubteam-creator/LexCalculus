namespace LexCalculus.Core.Messaging;

/// <summary>
/// Mesajlaşma real-time broadcast soyutlaması. MessageService send/delete
/// sonrası bu servise haber verir; implementasyon (Web katmanında SignalR)
/// recipient'a push eder. Test'te NoOp ile DI override edilir.
/// Charter §3 Karar 2 (real-time mesajlaşma), Faz 5.6.
/// </summary>
public interface IMessagingNotifier
{
    /// <summary>
    /// Bir mesajın recipient'a iletilmesi gerektiğini bildirir. Implementasyon
    /// mesajı yükleyip _Message partial render ederek client'a HTML push edebilir.
    /// SignalR connection yoksa client polling fallback ile yakalar.
    /// </summary>
    Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default);

    /// <summary>
    /// Bir mesajın silindiğini recipient'a bildirir. Client DOM'da mesajı
    /// '(Bu mesaj silindi)' placeholder'ına çevirir.
    /// </summary>
    Task NotifyMessageDeletedAsync(int recipientId, int conversationId, int messageId, CancellationToken ct = default);

    /// <summary>
    /// Mesaj admin tarafından gizlendiğini KONUŞMANIN HER İKİ KATILIMCISINA
    /// bildirir (sender de placeholder olarak görmeli, alıcı listede filter).
    /// senderId == ownerId; recipientId == karşı taraf. SignalR Groups.Groups
    /// ile tek call'da iki gruba broadcast.
    /// Faz 5.7 mesaj moderasyonu.
    /// </summary>
    Task NotifyMessageHiddenAsync(
        int senderId, int recipientId, int conversationId, int messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Bir kullanıcının bir conversation'ı okuduğunu O KULLANICININ tüm
    /// oturumlarına (user-{userId} grubu) bildirir. Multi-tab read-state
    /// senkronizasyonu için (#37). Faz 6.7. Detail handler şimdilik no-op;
    /// liste sayfası real-time unread badge tam çözümü Faz 7+.
    /// </summary>
    Task NotifyConversationReadAsync(int userId, int conversationId, CancellationToken ct = default);
}
