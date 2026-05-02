using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.ViewComponents;

/// <summary>
/// Admin sidebar'da bekleyen şikayet sayısını gösteren rozet.
/// User auth kontrolü yapılmaz — admin layout zaten [Authorize(Policy="AdminOnly")]
/// içinde render edilir. Faz 4.10 P2.
/// </summary>
public sealed class PendingContentReportsBadgeViewComponent : ViewComponent
{
    private readonly IContentReportService _reports;

    public PendingContentReportsBadgeViewComponent(IContentReportService reports)
    {
        _reports = reports;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await _reports.GetPendingCountAsync();
        return View(count);
    }
}
