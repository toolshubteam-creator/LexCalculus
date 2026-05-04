using LexCalculus.Core.Entities.Messaging;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Web.Infrastructure.Rendering;
using LexCalculus.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LexCalculus.Web.Controllers.Api;

/// <summary>
/// 1-1 mesajlaşma AJAX endpoint'i. Send + Delete + GetByConversation +
/// GetNewSince (polling) + MarkRead. Send/MarkRead CSRF + rate limit korumalı.
/// _Message partial server-side render edilir (XSS güvenli, sanitize servis
/// tarafında). Faz 5.5, charter Karar 1, 3, 4. Faz 5.6'da SignalR ile real-time.
/// </summary>
[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;
    private readonly IConversationService _conversations;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPartialRenderer _partial;
    private readonly IMediaStorage _storage;

    public MessagesController(
        IMessageService messages,
        IConversationService conversations,
        UserManager<ApplicationUser> userManager,
        IPartialRenderer partial,
        IMediaStorage storage)
    {
        _messages = messages;
        _conversations = conversations;
        _userManager = userManager;
        _partial = partial;
        _storage = storage;
    }

    [HttpPost("send")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("message")]
    public async Task<IActionResult> Send(
        [FromBody] SendMessageRequest? req, CancellationToken ct = default)
    {
        if (req is null || req.RecipientId <= 0 || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Geçersiz istek." });

        if (!TryGetUserId(out var senderId)) return Unauthorized();

        var result = await _messages.SendAsync(senderId, req.RecipientId, req.Body, ct);
        if (!result.Success || result.Message is null)
            return BadRequest(new { error = result.ErrorMessage ?? "Mesaj gönderilemedi." });

        var vm = await BuildVmAsync(result.Message.Id, senderId, ct);
        var html = await _partial.RenderAsync("_Message", vm, ct);

        return Ok(new
        {
            success = true,
            messageId = result.Message.Id,
            conversationId = result.Message.ConversationId,
            html
        });
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var actingUserId)) return Unauthorized();

        var result = await _messages.DeleteAsync(id, actingUserId, ct);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "Silinemedi." });

        return Ok(new { success = true });
    }

    [HttpGet("{conversationId:int}")]
    public async Task<IActionResult> GetByConversation(
        int conversationId, [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();

        var conv = await _conversations.GetByIdAsync(conversationId, viewerId, ct);
        if (conv is null) return NotFound();

        if (skip < 0) skip = 0;
        if (take <= 0 || take > 200) take = 50;

        var messages = await _messages.GetByConversationAsync(conversationId, viewerId, skip, take, ct);
        var total = await _messages.GetCountForConversationAsync(conversationId, ct);

        var vms = messages.Select(m => BuildVm(m, viewerId)).ToList();

        return Ok(new
        {
            messages = vms,
            hasMore = skip + take < total,
            total
        });
    }

    [HttpGet("{conversationId:int}/new")]
    public async Task<IActionResult> GetNewSince(
        int conversationId, [FromQuery] string? since,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();

        var conv = await _conversations.GetByIdAsync(conversationId, viewerId, ct);
        if (conv is null) return NotFound();

        // Defansif fallback: invalid/missing 'since' → son 1 dk
        DateTime sinceTime;
        if (string.IsNullOrEmpty(since)
            || !DateTime.TryParse(since, null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out sinceTime))
        {
            sinceTime = DateTime.UtcNow.AddMinutes(-1);
        }
        else
        {
            sinceTime = sinceTime.ToUniversalTime();
        }

        var newMessages = await _messages.GetNewerThanAsync(conversationId, viewerId, sinceTime, ct);

        var htmlList = new List<string>(newMessages.Count);
        foreach (var m in newMessages)
        {
            var vm = BuildVm(m, viewerId);
            htmlList.Add(await _partial.RenderAsync("_Message", vm, ct));
        }

        return Ok(new
        {
            messages = htmlList,
            latestAt = newMessages.Count == 0 ? (DateTime?)null : newMessages[^1].CreatedAt
        });
    }

    [HttpPost("{conversationId:int}/mark-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int conversationId, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var viewerId)) return Unauthorized();

        var result = await _conversations.MarkAsReadAsync(conversationId, viewerId, ct);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage ?? "İşlem başarısız." });

        return Ok(new { success = true });
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private bool TryGetUserId(out int userId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out userId);
    }

    private async Task<MessageViewModel> BuildVmAsync(int messageId, int viewerId, CancellationToken ct)
    {
        var msg = await _messages.GetByIdAsync(messageId, viewerId, ct)
                  ?? throw new InvalidOperationException(
                      $"Yeni eklenen mesaj fetch edilemedi (id={messageId}).");
        return BuildVm(msg, viewerId);
    }

    private MessageViewModel BuildVm(Message m, int viewerId)
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
    }
}

public sealed record SendMessageRequest(int RecipientId, string Body);
