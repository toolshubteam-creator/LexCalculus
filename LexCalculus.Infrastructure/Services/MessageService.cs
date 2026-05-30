using LexCalculus.Core.Entities.Email;
using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Messaging;
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
    private readonly IMessagingNotifier _notifier;
    private readonly ILogger<MessageService>? _logger;

    public MessageService(
        ApplicationDbContext ctx,
        IConversationService conversationService,
        ICommentSanitizer sanitizer,
        IActivityLogService activityLog,
        IMessagingNotifier notifier,
        ILogger<MessageService>? logger = null)
    {
        _ctx = ctx;
        _conversationService = conversationService;
        _sanitizer = sanitizer;
        _activityLog = activityLog;
        _notifier = notifier;
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

        // Faz 6.2 P2 — mesaj dijesti kuyruğu. Alıcının master (NotificationsEmailEnabled)
        // + granüler (EmailOnMessageDigest) tercihi açıksa kayıt eklenir; dolu olmayan
        // entry yaratmamak için send anında kontrol edilir. Job 5 dk pencere dolunca toplar.
        var pref = await _ctx.Users
            .Where(u => u.Id == recipientId)
            .Select(u => new
            {
                u.NotificationsEmailEnabled,
                Digest = u.Profile != null && u.Profile.EmailOnMessageDigest
            })
            .FirstOrDefaultAsync(ct);

        if (pref is { NotificationsEmailEnabled: true, Digest: true })
        {
            _ctx.EmailDigestEntries.Add(new EmailDigestEntry
            {
                UserId = recipientId,
                Type = EmailDigestType.Message,
                RelatedEntityId = message.Id,
                CreatedAt = now,
                IsSent = false
            });
            await _ctx.SaveChangesAsync(ct);
        }

        // Real-time broadcast (sessiz fail — polling fallback devreye girer)
        try
        {
            await _notifier.NotifyMessageReceivedAsync(recipientId, message.Id, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Notifier MessageReceived broadcast failed (mesaj kaydedildi): msgId={MessageId}",
                message.Id);
        }

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

        // Recipient = conversation'daki diğer katılımcı (acting user değil)
        var convIds = await _ctx.Conversations
            .Where(c => c.Id == message.ConversationId)
            .Select(c => new { c.User1Id, c.User2Id })
            .FirstOrDefaultAsync(ct);
        if (convIds is not null)
        {
            var recipientId = convIds.User1Id == actingUserId ? convIds.User2Id : convIds.User1Id;
            try
            {
                await _notifier.NotifyMessageDeletedAsync(
                    recipientId, message.ConversationId, message.Id, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Notifier MessageDeleted broadcast failed (silme uygulandı): msgId={MessageId}",
                    message.Id);
            }
        }

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

        // Faz 5.7 — IsModeratorHidden filter: alıcı için filter (görünmez),
        // sahip için liste'de kalır (view'da placeholder render eder).
        return await _ctx.Messages
            .Include(m => m.Sender).ThenInclude(u => u!.Profile)
            .Where(m => m.ConversationId == conversationId
                     && (!m.IsModeratorHidden || m.SenderId == viewerId))
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> GetCountForConversationAsync(int conversationId, CancellationToken ct = default)
        => _ctx.Messages.CountAsync(m => m.ConversationId == conversationId, ct);

    public async Task<IReadOnlyList<Message>> GetNewerThanAsync(
        int conversationId, int viewerId, DateTime since, CancellationToken ct = default)
    {
        var conv = await _conversationService.GetByIdAsync(conversationId, viewerId, ct);
        if (conv is null) return Array.Empty<Message>();

        // Polling: alıcının other-sender yeni mesajları. SenderId != viewerId
        // koşulu var, yani viewer her zaman alıcı — IsModeratorHidden mesajlar
        // hiç görünmez (alıcı için filter kuralı).
        return await _ctx.Messages
            .Include(m => m.Sender).ThenInclude(u => u!.Profile)
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != viewerId
                     && m.CreatedAt > since
                     && !m.IsModeratorHidden)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Message?> GetByIdAsync(int messageId, int viewerId, CancellationToken ct = default)
    {
        var msg = await _ctx.Messages
            .Include(m => m.Sender).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return null;

        var conv = await _conversationService.GetByIdAsync(msg.ConversationId, viewerId, ct);
        return conv is null ? null : msg;
    }
}
