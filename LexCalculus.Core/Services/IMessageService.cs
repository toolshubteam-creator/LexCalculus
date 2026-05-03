using LexCalculus.Core.Entities.Messaging;

namespace LexCalculus.Core.Services;

/// <summary>
/// Mesaj servisi. SendAsync yetki kontrolü ConversationService.GetOrCreateAsync
/// üzerinden yapılır (yetki tek yerde). Sanitize: CommentBodyProcessor reuse
/// (text + auto-link + sınırlı whitelist). Faz 5.4, charter Karar 1.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Mesaj gönder. Conversation yoksa GetOrCreate çağrılır (yetki kontrol burada).
    /// Body sanitize edilir (CommentBodyProcessor). LastMessageAt update.
    /// </summary>
    Task<MessageResult> SendAsync(
        int senderId, int recipientId, string rawBody, CancellationToken ct = default);

    /// <summary>
    /// Sahip soft delete (Body korunur, IsDeleted=true). Admin moderasyon Adım 5.7.
    /// </summary>
    Task<MessageResult> DeleteAsync(int messageId, int actingUserId, CancellationToken ct = default);

    /// <summary>
    /// Conversation mesajları, en yeniden eskiye sıralı (UI scroll için).
    /// Yetki: viewer katılımcı olmalı; değilse boş liste.
    /// </summary>
    Task<IReadOnlyList<Message>> GetByConversationAsync(
        int conversationId, int viewerId, int skip = 0, int take = 50,
        CancellationToken ct = default);

    Task<int> GetCountForConversationAsync(int conversationId, CancellationToken ct = default);
}

public sealed record MessageResult(bool Success, string? ErrorMessage, Message? Message);
