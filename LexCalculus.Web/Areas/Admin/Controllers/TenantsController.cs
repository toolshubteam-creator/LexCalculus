using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/tenants")]
public sealed class TenantsController : Controller
{
    private readonly ITenantAdminService _tenants;

    public TenantsController(ITenantAdminService tenants)
    {
        _tenants = tenants;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var items = await _tenants.GetAllAsync(includeDeleted, search, ct);
        var vm = new TenantListVm
        {
            Items = items,
            Search = search,
            IncludeDeleted = includeDeleted
        };

        ViewData["Title"] = "Hukuk Büroları";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hukuk Büroları", null)
        };
        return View(vm);
    }

    [HttpGet("yeni")]
    public async Task<IActionResult> Yeni(CancellationToken ct = default)
    {
        var available = await _tenants.GetAvailableUsersAsync(ct);
        var vm = new TenantCreateVm { AvailableOwners = available };

        ViewData["Title"] = "Yeni Hukuk Bürosu";
        ViewData["Breadcrumb"] = BuildBreadcrumb(("Yeni", null));
        return View(vm);
    }

    [HttpPost("yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(TenantCreateVm vm, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            vm.AvailableOwners = await _tenants.GetAvailableUsersAsync(ct);
            return View(vm);
        }

        try
        {
            var id = await _tenants.CreateAsync(
                new CreateTenantRequest(vm.Name, vm.Slug, vm.OwnerUserId!.Value), ct);
            TempData["AdminSuccess"] = $"✓ '{vm.Name}' tenant'ı oluşturuldu.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            vm.AvailableOwners = await _tenants.GetAvailableUsersAsync(ct);
            return View(vm);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detay(int id, CancellationToken ct = default)
    {
        var detail = await _tenants.GetByIdAsync(id, ct);
        if (detail == null) return NotFound();

        var available = await _tenants.GetAvailableUsersAsync(ct);
        var vm = new TenantDetailVm
        {
            Detail = detail,
            AvailableUsers = available
        };

        ViewData["Title"] = $"{detail.Name} — Detay";
        ViewData["Breadcrumb"] = BuildBreadcrumb((detail.Name, null));
        return View(vm);
    }

    [HttpGet("{id:int}/duzenle")]
    public async Task<IActionResult> Duzenle(int id, CancellationToken ct = default)
    {
        var detail = await _tenants.GetByIdAsync(id, ct);
        if (detail == null) return NotFound();

        var available = await _tenants.GetAvailableUsersAsync(ct);
        // Mevcut owner'ı candidate listesine ekle (henüz available'da yok çünkü TenantId set)
        var ownerOption = new UserOptionDto(detail.OwnerUserId, "", detail.OwnerUserName);
        var candidates = new List<UserOptionDto> { ownerOption };
        candidates.AddRange(available);

        var vm = new TenantEditVm
        {
            Id = detail.Id,
            Name = detail.Name,
            Slug = detail.Slug,
            OwnerUserId = detail.OwnerUserId,
            OwnerCandidates = candidates
        };

        ViewData["Title"] = $"{detail.Name} — Düzenle";
        ViewData["Breadcrumb"] = BuildBreadcrumb(
            (detail.Name, Url.Action(nameof(Detay), new { id })),
            ("Düzenle", null));
        return View(vm);
    }

    [HttpPost("{id:int}/duzenle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzenle(int id, TenantEditVm vm, CancellationToken ct = default)
    {
        if (id != vm.Id) return BadRequest();

        if (!ModelState.IsValid)
            return await ReloadEditView(vm, ct);

        try
        {
            await _tenants.UpdateAsync(id, new UpdateTenantRequest(vm.Name, vm.Slug, vm.OwnerUserId), ct);
            TempData["AdminSuccess"] = "✓ Tenant güncellendi.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await ReloadEditView(vm, ct);
        }
    }

    [HttpPost("{id:int}/sil")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sil(int id, CancellationToken ct = default)
    {
        try
        {
            await _tenants.SoftDeleteAsync(id, ct);
            TempData["AdminSuccess"] = "✓ Tenant silindi (üyelerin TenantId'si null'a çevrildi).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/uye-ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UyeEkle(int id, [FromForm] int yeniUyeId, CancellationToken ct = default)
    {
        try
        {
            await _tenants.AddMemberAsync(id, yeniUyeId, ct);
            TempData["AdminSuccess"] = "✓ Üye eklendi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost("{id:int}/uye-cikar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UyeCikar(int id, [FromForm] int uyeId, CancellationToken ct = default)
    {
        try
        {
            await _tenants.RemoveMemberAsync(id, uyeId, ct);
            TempData["AdminSuccess"] = "✓ Üye çıkarıldı.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }
        return RedirectToAction(nameof(Detay), new { id });
    }

    private List<(string Label, string? Url)> BuildBreadcrumb(params (string Label, string? Url)[] tail)
    {
        var crumbs = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Hukuk Büroları", Url.Action(nameof(Index)))
        };
        crumbs.AddRange(tail);
        return crumbs;
    }

    private async Task<IActionResult> ReloadEditView(TenantEditVm vm, CancellationToken ct)
    {
        var available = await _tenants.GetAvailableUsersAsync(ct);
        var detail = await _tenants.GetByIdAsync(vm.Id, ct);
        var ownerOption = detail != null
            ? new UserOptionDto(detail.OwnerUserId, "", detail.OwnerUserName)
            : null;
        var candidates = new List<UserOptionDto>();
        if (ownerOption != null) candidates.Add(ownerOption);
        candidates.AddRange(available);
        vm.OwnerCandidates = candidates;
        return View(vm);
    }
}
