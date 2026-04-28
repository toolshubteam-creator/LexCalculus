using LexCalculus.Core.Services;
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

    public LifeTablesController(ILifeTableAdminService admin)
    {
        _admin = admin;
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
