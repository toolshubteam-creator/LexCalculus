using LexCalculus.Core.Calculators;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Interfaces;
using LexCalculus.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/parametreler")]
public sealed class ParametersController : Controller
{
    private readonly IFormulaParameterService _service;
    private readonly IFormulaFreshnessChecker _freshness;
    private readonly ICalculatorRegistry _registry;
    private readonly UserManager<ApplicationUser> _userManager;

    public ParametersController(
        IFormulaParameterService service,
        IFormulaFreshnessChecker freshness,
        ICalculatorRegistry registry,
        UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _freshness = freshness;
        _registry = registry;
        _userManager = userManager;
    }

    private int GetCurrentUserIdOrThrow()
    {
        var idStr = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out var id))
            throw new InvalidOperationException("Admin kullanıcısı kimliği çözülemedi.");
        return id;
    }

    private void PopulateFormOptions(ParameterFormViewModel vm)
    {
        var slugOptions = new List<(string Value, string Label)> { ("*", "Global (tüm araçlar)") };
        slugOptions.AddRange(_registry.GetAll().OrderBy(m => m.Title).Select(m => (m.Slug, m.Title)));
        vm.ToolSlugOptions = slugOptions;
        vm.FrequencyOptions = FrequencyLabels.All;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ParameterListFilterViewModel filter, CancellationToken ct)
    {
        filter ??= new ParameterListFilterViewModel();

        var all = await _service.GetAllAsync(ct);
        var now = DateTime.UtcNow;

        var currentVersionIds = all
            .GroupBy(p => (p.ToolSlug, p.Key))
            .Select(g => g.OrderByDescending(p => p.EffectiveDate).First().Id)
            .ToHashSet();

        var allRows = all.Select(p => MapRow(p, now, currentVersionIds)).ToList();
        var staleCount = allRows.Count(r => r.IsStale);

        IEnumerable<ParameterListRowViewModel> q = allRows;
        if (!string.IsNullOrWhiteSpace(filter.ToolSlug))
            q = q.Where(r => r.ToolSlug == filter.ToolSlug);
        if (!string.IsNullOrWhiteSpace(filter.Key))
            q = q.Where(r => r.Key.Contains(filter.Key, StringComparison.OrdinalIgnoreCase));
        if (filter.OnlyStale)
            q = q.Where(r => r.IsStale);
        if (filter.OnlyCurrent)
            q = q.Where(r => r.IsCurrentVersion);

        var rows = q.ToList();

        var options = new List<(string Value, string Label)> { ("*", "Global (tüm araçlar)") };
        options.AddRange(_registry.GetAll()
            .OrderBy(m => m.Title)
            .Select(m => (m.Slug, m.Title)));

        var vm = new ParameterListPageViewModel
        {
            Filter = filter,
            Rows = rows,
            TotalCount = allRows.Count,
            FilteredCount = rows.Count,
            StaleCount = staleCount,
            ToolSlugOptions = options
        };

        ViewData["Title"] = "Parametreler";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Parametreler", null)
        };

        return View(vm);
    }

    [HttpGet("gecmis/{toolSlug}/{key}")]
    public async Task<IActionResult> History(string toolSlug, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toolSlug) || string.IsNullOrWhiteSpace(key))
            return NotFound();

        var versions = await _service.GetHistoryAsync(toolSlug, key, ct);
        if (versions.Count == 0) return NotFound();

        var now = DateTime.UtcNow;
        var currentId = versions.OrderByDescending(p => p.EffectiveDate).First().Id;
        var rows = versions.Select(p => MapRow(p, now, new HashSet<int> { currentId })).ToList();

        var vm = new ParameterHistoryViewModel
        {
            ToolSlug = toolSlug,
            Key = key,
            Versions = rows
        };

        ViewData["Title"] = $"Tarihçe — {toolSlug} / {key}";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Parametreler", Url.Action("Index", "Parameters", new { area = "Admin" })),
            ($"{toolSlug} / {key}", null)
        };

        return View(vm);
    }

    [HttpGet("yeni")]
    public IActionResult New()
    {
        var vm = new ParameterFormViewModel { Mode = ParameterFormMode.New };
        PopulateFormOptions(vm);

        ViewData["Title"] = "Yeni Parametre";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Parametreler", Url.Action("Index", "Parameters", new { area = "Admin" })),
            ("Yeni", null)
        };

        return View("Form", vm);
    }

    [HttpPost("yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ParameterFormViewModel vm, CancellationToken ct)
    {
        vm.Mode = ParameterFormMode.New;

        if (!ModelState.IsValid)
        {
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        if (await _service.ExistsAsync(vm.ToolSlug, vm.Key, vm.EffectiveDate, null, ct))
        {
            ModelState.AddModelError("",
                $"Bu kombinasyon zaten kayıtlı: {vm.ToolSlug} / {vm.Key} / {vm.EffectiveDate:dd.MM.yyyy}. " +
                "Farklı yürürlük tarihi seçin veya mevcut kaydı düzeltmeyi deneyin.");
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        var userId = GetCurrentUserIdOrThrow();
        var entity = new FormulaParameter
        {
            ToolSlug = vm.ToolSlug,
            Key = vm.Key,
            Value = vm.Value,
            EffectiveDate = vm.EffectiveDate,
            ExpectedUpdateFrequency = vm.ExpectedUpdateFrequency,
            LastUpdatedDate = vm.LastUpdatedDate,
            Source = vm.Source,
            Note = vm.Note,
            Notes = vm.Notes,
            CreatedByUserId = userId,
            IsAutoUpdated = false
        };

        await _service.AddAsync(entity, ct);

        TempData["AdminSuccess"] = $"Yeni parametre eklendi: {vm.ToolSlug} / {vm.Key}. Cache temizlendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("yeni-versiyon/{toolSlug}/{key}")]
    public async Task<IActionResult> NewVersion(string toolSlug, string key, CancellationToken ct)
    {
        var versions = await _service.GetHistoryAsync(toolSlug, key, ct);
        if (versions.Count == 0) return NotFound();

        var latest = versions.OrderByDescending(p => p.EffectiveDate).First();

        var vm = new ParameterFormViewModel
        {
            Mode = ParameterFormMode.NewVersion,
            ToolSlug = latest.ToolSlug,
            Key = latest.Key,
            Value = latest.Value,
            EffectiveDate = DateTime.UtcNow.Date,
            ExpectedUpdateFrequency = latest.ExpectedUpdateFrequency ?? "Yearly",
            LastUpdatedDate = DateTime.UtcNow.Date,
            Source = latest.Source,
            Note = latest.Note,
            Notes = latest.Notes
        };
        PopulateFormOptions(vm);

        ViewData["Title"] = $"Yeni Versiyon — {toolSlug} / {key}";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Parametreler", Url.Action("Index", "Parameters", new { area = "Admin" })),
            ($"{toolSlug} / {key}", Url.Action("History", new { toolSlug, key })),
            ("Yeni Versiyon", null)
        };

        return View("Form", vm);
    }

    [HttpPost("yeni-versiyon/{toolSlug}/{key}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewVersion(string toolSlug, string key, ParameterFormViewModel vm, CancellationToken ct)
    {
        vm.Mode = ParameterFormMode.NewVersion;
        vm.ToolSlug = toolSlug;
        vm.Key = key;

        if (!ModelState.IsValid)
        {
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        if (await _service.ExistsAsync(vm.ToolSlug, vm.Key, vm.EffectiveDate, null, ct))
        {
            ModelState.AddModelError(nameof(vm.EffectiveDate),
                $"Bu yürürlük tarihi için zaten bir versiyon var: {vm.EffectiveDate:dd.MM.yyyy}. Farklı tarih seçin.");
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        var userId = GetCurrentUserIdOrThrow();
        var entity = new FormulaParameter
        {
            ToolSlug = vm.ToolSlug,
            Key = vm.Key,
            Value = vm.Value,
            EffectiveDate = vm.EffectiveDate,
            ExpectedUpdateFrequency = vm.ExpectedUpdateFrequency,
            LastUpdatedDate = vm.LastUpdatedDate,
            Source = vm.Source,
            Note = vm.Note,
            Notes = vm.Notes,
            CreatedByUserId = userId,
            IsAutoUpdated = false
        };

        await _service.AddAsync(entity, ct);

        TempData["AdminSuccess"] = $"Yeni versiyon eklendi: {vm.ToolSlug} / {vm.Key} ({vm.EffectiveDate:dd.MM.yyyy}). Cache temizlendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("duzelt/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var all = await _service.GetAllAsync(ct);
        var p = all.FirstOrDefault(x => x.Id == id);
        if (p is null) return NotFound();

        var vm = new ParameterFormViewModel
        {
            Mode = ParameterFormMode.Edit,
            Id = p.Id,
            ToolSlug = p.ToolSlug,
            Key = p.Key,
            Value = p.Value,
            EffectiveDate = p.EffectiveDate,
            ExpectedUpdateFrequency = p.ExpectedUpdateFrequency ?? "Yearly",
            LastUpdatedDate = p.LastUpdatedDate,
            Source = p.Source,
            Note = p.Note,
            Notes = p.Notes
        };
        PopulateFormOptions(vm);

        ViewData["Title"] = $"Düzelt — {p.ToolSlug} / {p.Key}";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Parametreler", Url.Action("Index", "Parameters", new { area = "Admin" })),
            ($"Düzelt: {p.ToolSlug} / {p.Key}", null)
        };

        return View("Form", vm);
    }

    [HttpPost("duzelt/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ParameterFormViewModel vm, CancellationToken ct)
    {
        vm.Mode = ParameterFormMode.Edit;
        vm.Id = id;

        if (!ModelState.IsValid)
        {
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        if (await _service.ExistsAsync(vm.ToolSlug, vm.Key, vm.EffectiveDate, excludeId: id, ct))
        {
            ModelState.AddModelError("",
                "Bu (slug, key, tarih) kombinasyonu başka bir kayıtla çakışıyor.");
            PopulateFormOptions(vm);
            return View("Form", vm);
        }

        var userId = GetCurrentUserIdOrThrow();
        var entity = new FormulaParameter
        {
            Id = id,
            ToolSlug = vm.ToolSlug,
            Key = vm.Key,
            Value = vm.Value,
            EffectiveDate = vm.EffectiveDate,
            ExpectedUpdateFrequency = vm.ExpectedUpdateFrequency,
            LastUpdatedDate = vm.LastUpdatedDate,
            Source = vm.Source,
            Note = vm.Note,
            Notes = vm.Notes
        };

        await _service.UpdateAsync(entity, userId, ct);

        TempData["AdminSuccess"] = $"Parametre güncellendi: {vm.ToolSlug} / {vm.Key}. Cache temizlendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserIdOrThrow();
            await _service.SoftDeleteAsync(id, userId, ct);
            TempData["AdminSuccess"] = "Parametre silindi (soft delete). Cache temizlendi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = $"Silme başarısız: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    private ParameterListRowViewModel MapRow(FormulaParameter p, DateTime now, HashSet<int> currentIds)
        => new()
        {
            Id = p.Id,
            ToolSlug = p.ToolSlug,
            Key = p.Key,
            Value = p.Value,
            EffectiveDate = p.EffectiveDate,
            Frequency = p.ExpectedUpdateFrequency,
            FrequencyLabelTr = FrequencyLabels.ToTurkish(p.ExpectedUpdateFrequency),
            LastUpdatedDate = p.LastUpdatedDate,
            Source = p.Source,
            Notes = p.Notes,
            // Adım 3.3.4b bug fix: sadece (slug, key) için en yeni effective-dated
            // satır stale olabilir. Arşiv satırlar canlı hesaplamayı etkilemez.
            IsStale = currentIds.Contains(p.Id) && _freshness.IsStale(p, now),
            DaysUntilStale = _freshness.DaysUntilStale(p, now),
            IsCurrentVersion = currentIds.Contains(p.Id)
        };
}
