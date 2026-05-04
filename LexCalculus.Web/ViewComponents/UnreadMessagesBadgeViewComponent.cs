using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.ViewComponents;

/// <summary>
/// Üst menü "Mesajlar" linkinin yanında okunmamış mesaj sayım badge'i.
/// Anonim kullanıcı veya sayım=0 → boş render. ConversationService.GetUnreadCountAsync
/// reuse (n+1 query var, Faz 6+ optimize). Faz 5.5.
/// </summary>
public sealed class UnreadMessagesBadgeViewComponent : ViewComponent
{
    private readonly IConversationService _conversations;
    private readonly UserManager<ApplicationUser> _userManager;

    public UnreadMessagesBadgeViewComponent(
        IConversationService conversations,
        UserManager<ApplicationUser> userManager)
    {
        _conversations = conversations;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return View(0);

        var raw = _userManager.GetUserId(UserClaimsPrincipal);
        if (!int.TryParse(raw, out var userId))
            return View(0);

        var count = await _conversations.GetUnreadCountAsync(userId, HttpContext.RequestAborted);
        return View(count);
    }
}
