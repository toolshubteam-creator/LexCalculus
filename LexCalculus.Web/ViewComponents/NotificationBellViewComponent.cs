using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.ViewComponents;

public sealed class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBellViewComponent(
        INotificationService notifications,
        UserManager<ApplicationUser> userManager)
    {
        _notifications = notifications;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return Content(string.Empty);

        var idStr = _userManager.GetUserId(UserClaimsPrincipal);
        if (!int.TryParse(idStr, out var userId))
            return Content(string.Empty);

        var unreadCount = await _notifications.GetUnreadCountAsync(userId);
        return View(new NotificationBellViewModel(userId, unreadCount));
    }
}

public sealed record NotificationBellViewModel(int UserId, int UnreadCount);
