using LexCalculus.Core.Services;
using LexCalculus.Core.Services.Csv;
using LexCalculus.Web.Areas.Admin.Models.LifeTable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/lifetable")]
public sealed class LifeTablesController : Controller
{
    private readonly ILifeTableAdminService _admin;
    private readonly ILifeTableCsvParser _csvParser;

    public LifeTablesController(
        ILifeTableAdminService admin,
        ILifeTableCsvParser csvParser)
    {
        _admin = admin;
        _csvParser = csvParser;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tables = await _admin.GetAllAsync(ct);

        var items = tables.Select(t => new LifeTableRowViewModel
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            EffectiveDate = t.EffectiveDate,
            IsActive = t.IsActive,
            RowCount = t.Rows?.Count ?? 0,
            Source = t.Source
        }).ToList();

        var vm = new LifeTableListPageViewModel { Items = items };

        ViewData["Title"] = "Hayat Tabloları";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hayat Tabloları", null)
        };

        return View(vm);
    }

    [HttpGet("yeni")]
    public IActionResult New()
    {
        var vm = new LifeTableCreateViewModel();

        ViewData["Title"] = "Yeni Hayat Tablosu";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hayat Tabloları", Url.Action("Index")),
            ("Yeni", null)
        };

        return View(vm);
    }

    [HttpPost("yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(LifeTableCreateViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(vm);

        if (vm.CsvFile == null || vm.CsvFile.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.CsvFile), "CSV dosyası boş veya yüklenmedi.");
            return View(vm);
        }

        CsvParseResult parseResult;
        using (var stream = vm.CsvFile.OpenReadStream())
        {
            parseResult = await _csvParser.ParseAsync(stream, ct);
        }

        if (!parseResult.Success)
        {
            var topErrors = parseResult.Errors.Take(10);
            foreach (var err in topErrors)
            {
                ModelState.AddModelError("",
                    err.LineNumber > 0
                        ? $"Satır {err.LineNumber} ({err.Field}): {err.Message}"
                        : $"{err.Field}: {err.Message}");
            }
            if (parseResult.Errors.Count > 10)
            {
                ModelState.AddModelError("",
                    $"... ve {parseResult.Errors.Count - 10} daha (toplam {parseResult.Errors.Count} hata).");
            }
            return View(vm);
        }

        try
        {
            var newId = await _admin.CreateAsync(
                vm.Code, vm.Name, vm.EffectiveDate,
                vm.Source, vm.Note, parseResult.Rows, ct);

            TempData["AdminSuccess"] =
                $"✓ Hayat tablosu oluşturuldu: {vm.Code} ({parseResult.Rows.Count} satır). " +
                "Tablo şu an PASİF — aktif yapmak için listede 'Aktif yap' butonunu kullanın.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(vm.Code), ex.Message);
            return View(vm);
        }
    }

    [HttpPost("aktif/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        try
        {
            await _admin.ActivateAsync(id, ct);
            TempData["AdminSuccess"] =
                "✓ Hayat tablosu aktifleştirildi. Etkilenen hesaplayıcılar: Destekten Yoksun Kalma, " +
                "Maluliyet, Bakıcı Gideri, Geçici İş Göremezlik. Cache temizlendi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = $"Aktivasyon hatası: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
