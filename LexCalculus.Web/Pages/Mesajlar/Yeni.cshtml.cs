#nullable enable

using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Extensions;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Pages.Mesajlar;

/// <summary>
/// /mesajlar/yeni?recipient={id} — yeni konuşma için ilk mesaj formu.
/// Mevcut conversation varsa Detail'e redirect (akış DRY). Self message
/// (recipient == self) /mesajlar listesine redirect. Yetki son kontrol
/// SendAsync sırasında ConversationService.CanMessageAsync ile yapılır.
/// Faz 5.5.
/// </summary>
[Authorize]
public sealed class YeniModel : PageModel
{
    private readonly ApplicationDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userManager;

    public YeniModel(
        ApplicationDbContext ctx,
        UserManager<ApplicationUser> userManager)
    {
        _ctx = ctx;
        _userManager = userManager;
    }

    [BindProperty(SupportsGet = true, Name = "recipient")]
    public int RecipientId { get; set; }

    public string RecipientDisplayName { get; private set; } = "";
    public int MaxBodyLength => LexCalculus.Infrastructure.Services.MessageService.MaxRawBodyLength;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (RecipientId <= 0) return RedirectToPage("/Mesajlar/Index");

        if (!int.TryParse(_userManager.GetUserId(User), out var viewerId))
            return Challenge();

        if (viewerId == RecipientId) return RedirectToPage("/Mesajlar/Index");

        var recipient = await _ctx.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == RecipientId, ct);
        if (recipient is null || !recipient.IsActive) return NotFound();

        // Mevcut conversation varsa direkt detail'e (deterministic User1Id<User2Id)
        var u1 = Math.Min(viewerId, RecipientId);
        var u2 = Math.Max(viewerId, RecipientId);
        var existingId = await _ctx.Conversations
            .Where(c => c.User1Id == u1 && c.User2Id == u2)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId.HasValue)
            return RedirectToPage("/Mesajlar/Detail", new { id = existingId.Value });

        RecipientDisplayName = recipient.GetDisplayNameOrAnonymized();
        ViewData["Title"] = "Yeni Mesaj";
        return Page();
    }
}
