using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Models.Hesaplarim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Authorize]
[Route("hesaplarim")]
public sealed class HesaplarimController : Controller
{
    private readonly ICalculationHistoryService _history;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICalculatorRegistry _registry;

    public HesaplarimController(
        ICalculationHistoryService history,
        UserManager<ApplicationUser> userManager,
        ICalculatorRegistry registry)
    {
        _history = history;
        _userManager = userManager;
        _registry = registry;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] HesaplarimFilterViewModel filter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        filter ??= new HesaplarimFilterViewModel();

        var idStr = _userManager.GetUserId(User);
        if (!int.TryParse(idStr, out var userId)) return Forbid();

        const int pageSize = 25;

        var result = await _history.GetForUserAsync(
            userId, page, pageSize,
            toolSlugFilter: string.IsNullOrWhiteSpace(filter.ToolSlug) ? null : filter.ToolSlug,
            startDateUtc: filter.StartDate?.ToUniversalTime(),
            endDateUtc: filter.EndDate?.ToUniversalTime(),
            ct: ct);

        var usedSlugs = await _history.GetUsedToolSlugsForUserAsync(userId, ct);
        var titleMap = _registry.GetAll()
            .ToDictionary(m => m.Slug, m => m.Title, StringComparer.OrdinalIgnoreCase);
        var options = usedSlugs
            .Select(s => (Slug: s, Title: titleMap.TryGetValue(s, out var t) ? t : s))
            .ToList();

        var rows = result.Items.Select(h => new HesaplarimRowViewModel
        {
            Id = h.Id,
            ToolSlug = h.ToolSlug,
            CategorySlug = h.CategorySlug,
            ToolTitle = h.ToolTitle,
            CreatedAt = h.CreatedAt,
            TotalAmount = h.TotalAmount,
            Unit = h.Unit
        }).ToList();

        var vm = new HesaplarimListPageViewModel
        {
            Items = rows,
            Filter = filter,
            ToolOptions = options,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        ViewData["Title"] = "Hesaplarım";
        return View(vm);
    }
}
