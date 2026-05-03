using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// Mesaj servisi. Yetki: ConversationService.GetOrCreateAsync üzerinden
/// (single source of truth). Body: CommentBodyProcessor pattern reuse
/// (text + auto-link + sanitize). Faz 5.4.
/// </summary>
public sealed class MessageService : IMessageService
{
    /// <summary>Raw body max length (kullanıcı girişi); sanitize sonrası 2000.</summary>
    public const int MaxRawBodyLength = 1000;

    private readonly ApplicationDbContext _ctx;
    private readonly IConversationService _conversationService;
    private readonly ICommentSanitizer _sanitizer;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<MessageService>? _logger;

    public MessageService(
        ApplicationDbContext ctx,
        IConversationService conversationService,
        ICommentSanitizer sanitizer,
        IActivityLogService activityLog,
        ILogger<MessageService>? logger = null)
    {
        _ctx = ctx;
        _conversationService = conversationService;
        _sanitizer = sanitizer;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<MessageResult> SendAsync(
        int senderId, int recipientId, string rawBody, CancellationToken ct = default)
    {
        // Body validate (raw)
        if (string.IsNullOrWhiteSpace(rawBody))
            return new MessageResult(false, "Mesaj boş olamaz.", null);
        var trimmed = rawBody.Trim();
        if (trimmed.Length > MaxRawBodyLength)
            return new MessageResult(false,
                $"Mesaj {MaxRawBodyLength} karakteri aşamaz.", null);

        // Conversation GetOrCreate — yetki kontrol burada
        var convResult = await _conversationService.GetOrCreateAsync(senderId, recipientId, ct);
        if (!convResult.Success || convResult.Conversation is null)
            return new MessageResult(false, convResult.ErrorMessage ?? "Mesaj gönderilemedi.", null);

        var conv = convResult.Conversation;
        // Tracked conversation entity — LastMessageAt update için tracking gerekli
        var trackedConv = await _ctx.Conversations.FirstAsync(c => c.Id == conv.Id, ct);

        var sanitized = CommentBodyProcessor.Process(trimmed, _sanitizer);
        var now = DateTime.UtcNow;

        var message = new Message
        {
            ConversationId = trackedConv.Id,
            SenderId = senderId,
            Body = sanitized,
            CreatedAt = now,
            IsDeleted = false
        };
        _ctx.Messages.Add(message);
        trackedConv.LastMessageAt = now;

        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "Message.Send",
            entityType: nameof(Message),
            entityId: message.Id,
            description: $"Mesaj gönderildi conv={trackedConv.Id} sender={senderId}",
            metadata: new { message.Id, ConversationId = trackedConv.Id, SenderId = senderId },
            ct: ct);

        return new MessageResult(true, null, message);
    }

    public async Task<MessageResult> DeleteAsync(
        int messageId, int actingUserId, CancellationToken ct = default)
    {
        var message = await _ctx.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
            return new MessageResult(false, "Mesaj bulunamadı.", null);
        if (message.IsDeleted)
            return new MessageResult(true, null, message); // idempotent
        if (message.SenderId != actingUserId)
            return new MessageResult(false, "Yalnızca kendi mesajınızı silebilirsiniz.", null);

        message.IsDeleted = true;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "Message.Delete",
            entityType: nameof(Message),
            entityId: message.Id,
            description: $"Mesaj silindi id={message.Id} sender={actingUserId}",
            metadata: new { message.Id, message.ConversationId, ActingUserId = actingUserId },
            ct: ct);

        return new MessageResult(true, null, message);
    }

    public async Task<IReadOnlyList<Message>> GetByConversationAsync(
        int conversationId, int viewerId, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        // Yetki kontrolü: viewer conversation katılımcısı mı
        var conv = await _conversationService.GetByIdAsync(conversationId, viewerId, ct);
        if (conv is null) return Array.Empty<Message>();

        if (skip < 0) skip = 0;
        if (take <= 0 || take > 200) take = 50;

        return await _ctx.Messages
            .Include(m => m.Sender).ThenInclude(u => u!.Profile)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> GetCountForConversationAsync(int conversationId, CancellationToken ct = default)
        => _ctx.Messages.CountAsync(m => m.ConversationId == conversationId, ct);
}
