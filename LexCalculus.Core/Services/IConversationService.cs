using LexCalculus.Core.Entities.Messaging;

namespace LexCalculus.Core.Services;

/// <summary>
/// 1-1 mesajlaşma kanalı servisi. Yetki: bağlantı OR aynı tenant + NOT engelleme
/// (charter Faz 5 Karar 3, 4). Deterministic User1Id&lt;User2Id konvensiyonu
/// servis tarafında normalize edilir; DB composite unique index race koruması.
/// Faz 5.4.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// İki kullanıcı arasında conversation; mevcutsa döner, yoksa create.
    /// Yetki kontrol: bağlı OR aynı aktif tenant member + NOT engelleme.
    /// Yetki yoksa Result.Success=false (generic mesaj — engelleme leak yok).
    /// </summary>
    Task<ConversationResult> GetOrCreateAsync(
        int userAId, int userBId, CancellationToken ct = default);

    /// <summary>
    /// Conversation getir; viewer katılımcı değilse null (yetkisiz erişim engel).
    /// </summary>
    Task<Conversation?> GetByIdAsync(int conversationId, int viewerId, CancellationToken ct = default);

    /// <summary>
    /// User'ın conversation'ları, LastMessageAt DESC. Engellediği kullanıcılarla
    /// olan conversation'lar listeden gizli (sessiz). Her item için LastMessage
    /// preview + UnreadCount populate.
    /// </summary>
    Task<IReadOnlyList<ConversationListItem>> GetForUserAsync(
        int userId, CancellationToken ct = default);

    /// <summary>
    /// Viewer için conversation'ı okundu işaretle (User1LastReadAt veya
    /// User2LastReadAt güncelle). Viewer katılımcı değilse error.
    /// </summary>
    Task<ConversationResult> MarkAsReadAsync(
        int conversationId, int userId, CancellationToken ct = default);

    /// <summary>
    /// User'ın tüm conversation'larındaki toplam okunmamış mesaj sayısı.
    /// Bell icon / mesaj badge için. n+1 query var (Faz 6+ optimize).
    /// </summary>
    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// İki kullanıcı arası mesaj yetkisi (UI guard için): bağlantı OR aynı aktif
    /// tenant member; AND NOT engelleme. Yetki tek kaynak — GetOrCreateAsync da
    /// bu metodu kullanır. /uye/{slug} 'Mesaj Gönder' butonu görünürlüğü için
    /// Faz 5.5'te public expose edildi.
    /// </summary>
    Task<bool> CanMessageAsync(int userAId, int userBId, CancellationToken ct = default);
}

public sealed record ConversationResult(bool Success, string? ErrorMessage, Conversation? Conversation);

/// <summary>
/// /mesajlar liste sayfası için item DTO. OtherUser bilgisi server tarafında
/// resolve edilir (anonimize/inactive durumunda 'Silinmiş Kullanıcı' fallback).
/// </summary>
public sealed record ConversationListItem(
    int ConversationId,
    int OtherUserId,
    string OtherDisplayName,
    string? OtherSlug,
    string? OtherAvatarUrl,
    DateTime LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount);
