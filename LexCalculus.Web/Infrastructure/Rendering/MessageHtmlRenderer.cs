using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Storage;
using LexCalculus.Web.Models;

namespace LexCalculus.Web.Infrastructure.Rendering;

/// <inheritdoc cref="IMessageHtmlRenderer"/>
public sealed class MessageHtmlRenderer : IMessageHtmlRenderer
{
    private readonly IPartialRenderer _partial;
    private readonly IMediaStorage _storage;

    public MessageHtmlRenderer(IPartialRenderer partial, IMediaStorage storage)
    {
        _partial = partial;
        _storage = storage;
    }

    public MessageViewModel BuildViewModel(Message m, int viewerId)
    {
        // Anonimize/inactive gönderen → avatar gizli, displayName "Silinmiş Kullanıcı"
        // (GetDisplayNameOrAnonymized tek-kaynak fallback).
        var senderActive = m.Sender is { IsActive: true };
        var avatarPath = senderActive ? m.Sender.Profile?.AvatarUrl : null;

        return new MessageViewModel
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderDisplayName = m.Sender.GetDisplayNameOrAnonymized(),
            SenderAvatarUrl = string.IsNullOrEmpty(avatarPath)
                ? null
                : _storage.GetPublicUrl(avatarPath),
            Body = m.Body,
            CreatedAt = m.CreatedAt,
            IsDeleted = m.IsDeleted,
            IsModeratorHidden = m.IsModeratorHidden,
            IsOwnMessage = m.SenderId == viewerId
        };
    }

    public Task<string> RenderForViewerAsync(Message message, int viewerId, CancellationToken ct = default)
        => _partial.RenderAsync("_Message", BuildViewModel(message, viewerId), ct);
}
