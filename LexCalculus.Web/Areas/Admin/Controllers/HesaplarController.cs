using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.Hesaplar;
using LexCalculus.Web.Infrastructure.Hesaplarim;
using LexCalculus.Web.Models.Hesaplarim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/hesaplar")]
public sealed class HesaplarController : Controller
{
    private readonly ICalculationHistoryService _history;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICalculatorRegistry _registry;

    public HesaplarController(
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
        [FromQuery] AdminHesaplarFilterViewModel filter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        filter ??= new AdminHesaplarFilterViewModel();
        const int pageSize = 25;

        var result = await _history.GetAllPaginatedAsync(
            page, pageSize,
            toolSlugFilter: string.IsNullOrWhiteSpace(filter.ToolSlug) ? null : filter.ToolSlug,
            userIdFilter: filter.UserId,
            startDateUtc: filter.StartDate?.ToUniversalTime(),
            endDateUtc: filter.EndDate?.ToUniversalTime(),
            ct: ct);

        var userIds = result.Items.Select(h => h.UserId).Distinct().ToList();
        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.Id);

        var rows = result.Items.Select(h =>
        {
            string display;
            if (userMap.TryGetValue(h.UserId, out var u))
            {
                display = !string.IsNullOrWhiteSpace(u.FullName)
                    ? $"{u.Email} ({u.FullName})"
                    : u.Email ?? $"User #{h.UserId}";
            }
            else
            {
                display = $"User #{h.UserId} (silinmiş)";
            }

            return new AdminHesaplarRowViewModel
            {
                Id = h.Id,
                ToolSlug = h.ToolSlug,
                CategorySlug = h.CategorySlug,
                ToolTitle = h.ToolTitle,
                CreatedAt = h.CreatedAt,
                TotalAmount = h.TotalAmount,
                Unit = h.Unit,
                UserId = h.UserId,
                UserDisplay = display
            };
        }).ToList();

        var titleMap = _registry.GetAll()
            .ToDictionary(m => m.Slug, m => m.Title, StringComparer.OrdinalIgnoreCase);
        var toolOptions = titleMap
            .OrderBy(kv => kv.Value)
            .Select(kv => (Slug: kv.Key, Title: kv.Value))
            .ToList();

        var historyUserIds = await _history.GetUsersWithHistoryAsync(ct);
        var historyUsers = await _userManager.Users
            .Where(u => historyUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToListAsync(ct);
        var userOptions = historyUsers
            .OrderBy(u => u.Email)
            .Select(u => (
                Id: u.Id,
                Display: !string.IsNullOrWhiteSpace(u.FullName)
                    ? $"{u.Email} ({u.FullName})"
                    : u.Email ?? $"User #{u.Id}"))
            .ToList();

        var vm = new AdminHesaplarListPageViewModel
        {
            Items = rows,
            Filter = filter,
            ToolOptions = toolOptions,
            UserOptions = userOptions!,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        ViewData["Title"] = "Hesap Geçmişi (Admin)";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hesap Geçmişi", null)
        };

        if (HttpContext.Session.GetString("AdminHesaplarKvkkSeen") == null)
        {
            ViewData["ShowKvkkBanner"] = true;
            HttpContext.Session.SetString("AdminHesaplarKvkkSeen", "1");
        }

        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var entry = await _history.GetByIdForAdminAsync(id, ct);
        if (entry == null) return NotFound();

        var user = await _userManager.FindByIdAsync(entry.UserId.ToString());
        var userDisplay = user != null
            ? (!string.IsNullOrWhiteSpace(user.FullName)
                ? $"{user.Email} ({user.FullName})"
                : user.Email ?? $"User #{entry.UserId}")
            : $"User #{entry.UserId} (silinmiş)";

        var inputFields = JsonViewerHelper.ParseTopLevel(entry.InputJson);
        var outputFields = JsonViewerHelper.ParseTopLevel(entry.OutputJson);
        var outputPretty = JsonViewerHelper.PrettyPrint(entry.OutputJson);

        var vm = new HesaplarimDetailViewModel
        {
            Id = entry.Id,
            ToolSlug = entry.ToolSlug,
            CategorySlug = entry.CategorySlug,
            ToolTitle = entry.ToolTitle,
            CreatedAt = entry.CreatedAt,
            TotalAmount = entry.TotalAmount,
            Unit = entry.Unit,
            InputFields = inputFields,
            OutputFields = outputFields,
            OutputJsonPretty = outputPretty,
            RestoreUrl = string.Empty
        };

        ViewData["Title"] = $"{entry.ToolTitle} — Admin Detay";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hesap Geçmişi", Url.Action("Index", new { area = "Admin" })),
            ($"#{entry.Id}", null)
        };
        ViewData["AdminUserDisplay"] = userDisplay;
        ViewData["IsAdminView"] = true;

        return View("~/Views/Hesaplarim/Detail.cshtml", vm);
    }
}
