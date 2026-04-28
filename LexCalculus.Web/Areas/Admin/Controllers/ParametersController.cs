using LexCalculus.Core.Calculators;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
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

    public ParametersController(
        IFormulaParameterService service,
        IFormulaFreshnessChecker freshness,
        ICalculatorRegistry registry)
    {
        _service = service;
        _freshness = freshness;
        _registry = registry;
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
            IsStale = _freshness.IsStale(p, now),
            DaysUntilStale = _freshness.DaysUntilStale(p, now),
            IsCurrentVersion = currentIds.Contains(p.Id)
        };
}
