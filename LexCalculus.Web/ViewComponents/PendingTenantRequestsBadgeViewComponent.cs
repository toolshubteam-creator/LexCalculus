using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.ViewComponents;

/// <summary>
/// Admin sidebar'da bekleyen tenant talebi sayısını gösteren rozet.
/// User authentication kontrolü yapılmaz — admin layout zaten [Authorize(Policy="AdminOnly")]
/// içinde render edilir.
/// </summary>
public sealed class PendingTenantRequestsBadgeViewComponent : ViewComponent
{
    private readonly ITenantRequestService _requests;

    public PendingTenantRequestsBadgeViewComponent(ITenantRequestService requests)
    {
        _requests = requests;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await _requests.GetPendingCountAsync();
        return View(count);
    }
}
