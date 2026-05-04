#nullable enable

using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexCalculus.Web.Pages.Mesajlar;

/// <summary>
/// /mesajlar — viewer'ın conversation listesi. LastMessageAt DESC sıralı,
/// engellediği kullanıcılarla olan konuşmalar gizli (ConversationService filter).
/// Her item: other user + son mesaj preview + UnreadCount badge. Faz 5.5.
/// </summary>
[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IConversationService _conversations;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(
        IConversationService conversations,
        UserManager<ApplicationUser> userManager)
    {
        _conversations = conversations;
        _userManager = userManager;
    }

    public IReadOnlyList<ConversationListItem> Conversations { get; private set; }
        = Array.Empty<ConversationListItem>();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!int.TryParse(_userManager.GetUserId(User), out var userId))
            return Challenge();

        Conversations = await _conversations.GetForUserAsync(userId, ct);

        ViewData["Title"] = "Mesajlar";
        return Page();
    }
}
