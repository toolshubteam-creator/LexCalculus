using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.PostCategories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/kategoriler")]
public sealed class PostCategoriesController : Controller
{
    private readonly IPostCategoryService _service;

    public PostCategoriesController(IPostCategoryService service)
    {
        _service = service;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var items = await _service.GetAllAsync(ct);
        ViewData["Title"] = "Kategoriler";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kategoriler", null)
        };
        return View(new PostCategoryListVm { Items = items });
    }

    [HttpGet("yeni")]
    public IActionResult Yeni()
    {
        ViewData["Title"] = "Yeni Kategori";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kategoriler", Url.Action(nameof(Index))),
            ("Yeni", null)
        };
        return View(new PostCategoryFormVm());
    }

    [HttpPost("yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(PostCategoryFormVm vm, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(vm);

        var input = new PostCategoryInput(vm.Name, vm.Description, vm.DisplayOrder, vm.IsActive);
        var result = await _service.CreateAsync(input, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "İşlem başarısız.");
            return View(vm);
        }

        TempData["AdminSuccess"] = $"✓ Kategori oluşturuldu: {result.Category!.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/duzenle")]
    public async Task<IActionResult> Duzenle(int id, CancellationToken ct = default)
    {
        var category = await _service.GetByIdAsync(id, ct);
        if (category is null) return NotFound();

        ViewData["Title"] = $"Kategori #{id} — Düzenle";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kategoriler", Url.Action(nameof(Index))),
            (category.Name, null)
        };
        return View(new PostCategoryFormVm
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        });
    }

    [HttpPost("{id:int}/duzenle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(int id, PostCategoryFormVm vm, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return View(vm);

        var input = new PostCategoryInput(vm.Name, vm.Description, vm.DisplayOrder, vm.IsActive);
        var result = await _service.UpdateAsync(id, input, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "İşlem başarısız.");
            return View(vm);
        }

        TempData["AdminSuccess"] = $"✓ Kategori güncellendi: {result.Category!.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/devredisi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevreDisi(int id, CancellationToken ct = default)
    {
        var result = await _service.DeactivateAsync(id, ct);
        if (result.Success)
            TempData["AdminSuccess"] = "✓ Kategori devre dışı bırakıldı.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/etkinlestir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Etkinlestir(int id, CancellationToken ct = default)
    {
        var result = await _service.ReactivateAsync(id, ct);
        if (result.Success)
            TempData["AdminSuccess"] = "✓ Kategori etkinleştirildi.";
        else
            TempData["AdminError"] = result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Index));
    }
}
