using LexCalculus.Core.Admin.Dashboard;
using LexCalculus.Core.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class HomeController : Controller
{
    private readonly IDashboardSummaryService _summary;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        IDashboardSummaryService summary,
        UserManager<ApplicationUser> userManager)
    {
        _summary = summary;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var idStr = _userManager.GetUserId(User);
        int.TryParse(idStr, out var adminId);

        var summary = await _summary.GetSummaryAsync(adminId, ct);

        ViewData["Title"] = "Dashboard";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", null)
        };

        return View(summary);
    }
}
