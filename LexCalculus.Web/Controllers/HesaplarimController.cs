using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Infrastructure.Hesaplarim;
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

        var user = await _userManager.FindByIdAsync(idStr!);
        var hasTenant = user?.TenantId.HasValue ?? false;

        // Bireysel kullanıcılar için scope anlamsız — "all"a sabitle.
        var effectiveScope = hasTenant ? (filter.Scope ?? "all") : "all";

        const int pageSize = 25;

        var result = await _history.GetForUserAsync(
            userId, page, pageSize,
            toolSlugFilter: string.IsNullOrWhiteSpace(filter.ToolSlug) ? null : filter.ToolSlug,
            startDateUtc: filter.StartDate?.ToUniversalTime(),
            endDateUtc: filter.EndDate?.ToUniversalTime(),
            scope: effectiveScope,
            ct: ct);

        var usedSlugs = await _history.GetUsedToolSlugsForUserAsync(userId, ct);
        var titleMap = _registry.GetAll()
            .ToDictionary(m => m.Slug, m => m.Title, StringComparer.OrdinalIgnoreCase);
        var options = usedSlugs
            .Select(s => (Slug: s, Title: titleMap.TryGetValue(s, out var t) ? t : s))
            .ToList();

        // Paylaşılan satırlar için sahibi UserName lookup'ı (current user dışındakiler).
        var ownerIds = result.Items
            .Where(h => h.UserId != userId)
            .Select(h => h.UserId)
            .Distinct()
            .ToList();
        var nameMap = ownerIds.Count > 0
            ? await _history.GetUserNamesAsync(ownerIds, ct)
            : new Dictionary<int, string>();

        var rows = result.Items.Select(h => new HesaplarimRowViewModel
        {
            Id = h.Id,
            ToolSlug = h.ToolSlug,
            CategorySlug = h.CategorySlug,
            ToolTitle = h.ToolTitle,
            CreatedAt = h.CreatedAt,
            TotalAmount = h.TotalAmount,
            Unit = h.Unit,
            OwnerUserId = h.UserId,
            OwnerUserName = h.UserId == userId
                ? null
                : (nameMap.TryGetValue(h.UserId, out var n) ? n : $"#{h.UserId}"),
            IsShared = h.TenantId.HasValue,
            IsMine = h.UserId == userId
        }).ToList();

        filter.Scope = effectiveScope;
        var vm = new HesaplarimListPageViewModel
        {
            Items = rows,
            Filter = filter,
            ToolOptions = options,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
            HasTenant = hasTenant
        };

        ViewData["Title"] = "Hesaplarım";
        ViewData["NoIndex"] = true;
        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var idStr = _userManager.GetUserId(User);
        if (!int.TryParse(idStr, out var userId)) return Forbid();

        var entry = await _history.GetByIdForUserAsync(id, userId, ct);
        if (entry == null) return NotFound();

        var user = await _userManager.FindByIdAsync(idStr!);
        var hasTenant = user?.TenantId.HasValue ?? false;

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
            RestoreUrl = $"/hesapla/{entry.CategorySlug}/{entry.ToolSlug}?restore={entry.Id}",
            IsMine = entry.UserId == userId,
            HasTenant = hasTenant,
            IsShared = entry.TenantId.HasValue
        };

        ViewData["Title"] = $"{entry.ToolTitle} — Hesap Detayı";
        ViewData["NoIndex"] = true;
        return View(vm);
    }

    /// <summary>
    /// Hesabın paylaşım durumunu toggle et (post-hoc).
    /// share=true → CalculationHistory.TenantId = currentUser.TenantId
    /// share=false → null. Sadece sahibi togglelar.
    /// </summary>
    [HttpPost("{id:int}/paylasim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaylasimToggle(
        int id, [FromForm] bool share, CancellationToken ct)
    {
        var idStr = _userManager.GetUserId(User);
        if (!int.TryParse(idStr, out var userId)) return Forbid();

        var ok = await _history.SetSharingAsync(id, userId, share, ct);
        if (!ok)
            TempData["Error"] = "Paylaşım güncellenemedi (hesap bulunamadı veya tenant üyesi değilsiniz).";
        else
            TempData["Success"] = share
                ? "✓ Hesap ekibinize paylaşıldı."
                : "✓ Hesap ekipten kaldırıldı.";

        return RedirectToAction(nameof(Detail), new { id });
    }
}
