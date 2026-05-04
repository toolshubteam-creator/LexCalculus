#nullable enable

using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Pages.Mesajlar;

/// <summary>
/// /mesajlar/{id} — tek konuşma detayı. Mesajlar en yeni 50 (DESC fetch +
/// view'da reverse → eski üstte). Sayfa açılışında otomatik MarkAsReadAsync.
/// JS polling 30 sn ile yeni mesajlar canlı eklenir. Faz 5.5; SignalR Faz 5.6.
/// </summary>
[Authorize]
public sealed class DetailModel : PageModel
{
    private readonly IConversationService _conversations;
    private readonly IMessageService _messages;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMediaStorage _storage;

    public DetailModel(
        IConversationService conversations,
        IMessageService messages,
        UserManager<ApplicationUser> userManager,
        IMediaStorage storage)
    {
        _conversations = conversations;
        _messages = messages;
        _userManager = userManager;
        _storage = storage;
    }

    public int ConversationId { get; private set; }
    public int OtherUserId { get; private set; }
    public string OtherDisplayName { get; private set; } = "";
    public string? OtherSlug { get; private set; }
    public string? OtherAvatarUrl { get; private set; }
    public List<MessageViewModel> Messages { get; private set; } = new();
    public bool HasMore { get; private set; }
    public int MaxBodyLength => LexCalculus.Infrastructure.Services.MessageService.MaxRawBodyLength;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        if (!int.TryParse(_userManager.GetUserId(User), out var viewerId))
            return Challenge();

        var conv = await _conversations.GetByIdAsync(id, viewerId, ct);
        if (conv is null) return NotFound();

        ConversationId = conv.Id;

        var otherUser = conv.User1Id == viewerId ? conv.User2 : conv.User1;
        OtherUserId = otherUser.Id;
        OtherDisplayName = otherUser.GetDisplayNameOrAnonymized();
        OtherSlug = otherUser.GetPublicSlugOrNull();
        var otherAvatarPath = otherUser.IsAnonymizedOrInactive() ? null : otherUser.Profile?.AvatarUrl;
        OtherAvatarUrl = string.IsNullOrEmpty(otherAvatarPath)
            ? null
            : _storage.GetPublicUrl(otherAvatarPath);

        var raw = await _messages.GetByConversationAsync(id, viewerId, 0, 50, ct);
        var total = await _messages.GetCountForConversationAsync(id, ct);
        HasMore = total > raw.Count;

        Messages = raw.Select(m =>
        {
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
        }).ToList();

        // Sayfa açılışında okundu işaretle (other tarafı için unread → 0)
        await _conversations.MarkAsReadAsync(id, viewerId, ct);

        ViewData["Title"] = $"Mesajlar — {OtherDisplayName}";
        return Page();
    }
}
