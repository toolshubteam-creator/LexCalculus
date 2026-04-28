using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Notifications;
using LexCalculus.Web.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Authorize]
[Route("bildirimler")]
public sealed class NotificationsController : Controller
{
    private readonly INotificationService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(
        INotificationService service,
        UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        const int pageSize = 25;

        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        var allRecent = await _service.GetForUserAsync(
            userId.Value, pageSize * 4, unreadOnly, ct);

        var totalCount = allRecent.Count;
        var pagedItems = allRecent
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var vm = new NotificationsListViewModel
        {
            Items = pagedItems,
            UnreadOnly = unreadOnly,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            UnreadCount = await _service.GetUnreadCountAsync(userId.Value, ct)
        };

        ViewData["Title"] = "Bildirimler";
        ViewData["NoIndex"] = true;
        return View(vm);
    }

    [HttpGet("sayim")]
    [Produces("application/json")]
    public async Task<IActionResult> Sayim(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Json(new { unreadCount = 0 });

        var count = await _service.GetUnreadCountAsync(userId.Value, ct);
        return Json(new { unreadCount = count });
    }

    [HttpGet("onizleme")]
    [Produces("application/json")]
    public async Task<IActionResult> Onizleme(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Json(new { items = Array.Empty<object>() });

        var items = await _service.GetForUserAsync(userId.Value, 10, false, ct);

        var payload = items.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            body = n.Body,
            link = n.Link,
            type = n.Type.ToString(),
            iconHint = n.IconHint,
            isRead = n.IsRead,
            createdAt = n.CreatedAt.ToString("O")
        });

        return Json(new { items = payload });
    }

    [HttpPost("oku/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Oku(int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        try
        {
            await _service.MarkAsReadAsync(id, userId.Value, ct);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true });

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("tumunu-oku")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TumunuOku(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Forbid();

        var marked = await _service.MarkAllAsReadAsync(userId.Value, ct);

        TempData["NotifSuccess"] = $"{marked} bildirim okundu olarak işaretlendi.";
        return RedirectToAction(nameof(Index));
    }

    private int? GetCurrentUserId()
    {
        var idStr = _userManager.GetUserId(User);
        return int.TryParse(idStr, out var id) ? id : null;
    }
}
