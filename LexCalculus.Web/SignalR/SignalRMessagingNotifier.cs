using LexCalculus.Core.Extensions;
using LexCalculus.Core.Messaging;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Web.Infrastructure.Rendering;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.SignalR;

/// <summary>
/// IMessagingNotifier'ın SignalR implementasyonu. MessageService Send/Delete
/// çağrılarında recipient'ın user-{id} grubuna push eder. Mesaj VM render
/// _Message partial üzerinden server-side yapılır (sender/recipient
/// perspektifi: recipient için IsOwnMessage=false).
///
/// Sessiz fail: Hub down, recipient bağlı değil, vs. durumunda exception
/// fırlatılır ama servis tarafında try/catch ile yakalanır → polling
/// fallback devreye girer.
/// Faz 5.6.
/// </summary>
public sealed class SignalRMessagingNotifier : IMessagingNotifier
{
    private readonly IHubContext<MessagesHub> _hubContext;
    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;
    private readonly IPartialRenderer _partial;
    private readonly ILogger<SignalRMessagingNotifier>? _logger;

    public SignalRMessagingNotifier(
        IHubContext<MessagesHub> hubContext,
        ApplicationDbContext ctx,
        IMediaStorage storage,
        IPartialRenderer partial,
        ILogger<SignalRMessagingNotifier>? logger = null)
    {
        _hubContext = hubContext;
        _ctx = ctx;
        _storage = storage;
        _partial = partial;
        _logger = logger;
    }

    public async Task NotifyMessageReceivedAsync(int recipientId, int messageId, CancellationToken ct = default)
    {
        var message = await _ctx.Messages
            .Include(m => m.Sender).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null)
        {
            _logger?.LogWarning(
                "SignalR notifier: message {MessageId} not found, broadcast atlandı.", messageId);
            return;
        }

        var senderActive = message.Sender is { IsActive: true };
        var avatarPath = senderActive ? message.Sender.Profile?.AvatarUrl : null;

        var vm = new MessageViewModel
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderDisplayName = message.Sender.GetDisplayNameOrAnonymized(),
            SenderAvatarUrl = string.IsNullOrEmpty(avatarPath)
                ? null
                : _storage.GetPublicUrl(avatarPath),
            Body = message.Body,
            CreatedAt = message.CreatedAt,
            IsDeleted = message.IsDeleted,
            IsModeratorHidden = message.IsModeratorHidden,
            IsOwnMessage = false   // recipient için karşı tarafın mesajı
        };

        var html = await _partial.RenderAsync("_Message", vm, ct);

        await _hubContext.Clients
            .Group(MessagesHub.GroupName(recipientId))
            .SendAsync("MessageReceived", new
            {
                conversationId = message.ConversationId,
                messageId = message.Id,
                html
            }, ct);
    }

    public async Task NotifyMessageDeletedAsync(
        int recipientId, int conversationId, int messageId, CancellationToken ct = default)
    {
        await _hubContext.Clients
            .Group(MessagesHub.GroupName(recipientId))
            .SendAsync("MessageDeleted", new
            {
                conversationId,
                messageId
            }, ct);
    }

    public async Task NotifyMessageHiddenAsync(
        int senderId, int recipientId, int conversationId, int messageId,
        CancellationToken ct = default)
    {
        // Hem gönderene (kendi gizli mesajını placeholder olarak görmeli)
        // hem de alıcıya (recipient için liste'den filter, açık sayfa varsa
        // anlık placeholder) tek call'da broadcast.
        await _hubContext.Clients
            .Groups(MessagesHub.GroupName(senderId), MessagesHub.GroupName(recipientId))
            .SendAsync("MessageHidden", new
            {
                conversationId,
                messageId
            }, ct);
    }

    public async Task NotifyConversationReadAsync(
        int userId, int conversationId, CancellationToken ct = default)
    {
        // Kullanıcının kendi tüm oturumlarına (user-{id} grubu) okundu sinyali (#37).
        // Sessiz fail: Hub down / bağlantı yok → mark-read kaydı zaten yapıldı.
        try
        {
            await _hubContext.Clients
                .Group(MessagesHub.GroupName(userId))
                .SendAsync("ConversationRead", new { conversationId }, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "SignalR ConversationRead broadcast başarısız: user={UserId} conv={ConvId}",
                userId, conversationId);
        }
    }
}
