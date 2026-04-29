using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.TenantTalepleri;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/tenant-talepleri")]
public sealed class TenantTalepleriController : Controller
{
    private readonly ITenantRequestService _requests;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantTalepleriController(
        ITenantRequestService requests,
        UserManager<ApplicationUser> userManager)
    {
        _requests = requests;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] TenantRequestStatus? status = null,
        CancellationToken ct = default)
    {
        var defaultStatus = status ?? TenantRequestStatus.Pending;
        var items = await _requests.GetAllAsync(defaultStatus, ct);
        var vm = new TenantTalepleriListVm
        {
            Items = items,
            StatusFilter = defaultStatus
        };

        ViewData["Title"] = "Tenant Talepleri";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Tenant Talepleri", null)
        };
        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detay(int id, CancellationToken ct = default)
    {
        var detail = await _requests.GetByIdAsync(id, ct);
        if (detail == null) return NotFound();

        var vm = new TenantTalepleriDetailVm
        {
            Detail = detail,
            Approve = new TenantTalepleriApproveVm
            {
                FinalName = detail.ProposedName,
                FinalSlug = detail.ProposedSlug
            }
        };

        ViewData["Title"] = $"Talep #{id} — Detay";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Tenant Talepleri", Url.Action(nameof(Index))),
            ($"#{id}", null)
        };
        return View(vm);
    }

    [HttpPost("{id:int}/onayla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Onayla(int id, TenantTalepleriApproveVm approve, CancellationToken ct = default)
    {
        var adminUserId = int.Parse(_userManager.GetUserId(User) ?? "0");

        if (!ModelState.IsValid)
            return await ReloadDetail(id, approve, null, ct);

        try
        {
            await _requests.ApproveAsync(id, adminUserId,
                new ApproveTenantRequestInput(approve.FinalName, approve.FinalSlug), ct);

            // Tenant detay sayfasına yönlendir
            var detail = await _requests.GetByIdAsync(id, ct);
            TempData["AdminSuccess"] = "✓ Talep onaylandı, tenant oluşturuldu.";
            if (detail?.CreatedTenantId is int tId)
                return RedirectToAction("Detay", "Tenants", new { id = tId });
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await ReloadDetail(id, approve, null, ct);
        }
    }

    [HttpPost("{id:int}/reddet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reddet(int id, TenantTalepleriRejectVm reject, CancellationToken ct = default)
    {
        var adminUserId = int.Parse(_userManager.GetUserId(User) ?? "0");

        if (!ModelState.IsValid)
            return await ReloadDetail(id, null, reject, ct);

        try
        {
            await _requests.RejectAsync(id, adminUserId, reject.RejectionReason, ct);
            TempData["AdminSuccess"] = "✓ Talep reddedildi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await ReloadDetail(id, null, reject, ct);
        }
    }

    private async Task<IActionResult> ReloadDetail(
        int id, TenantTalepleriApproveVm? approve, TenantTalepleriRejectVm? reject,
        CancellationToken ct)
    {
        var detail = await _requests.GetByIdAsync(id, ct);
        if (detail == null) return NotFound();
        var vm = new TenantTalepleriDetailVm
        {
            Detail = detail,
            Approve = approve ?? new TenantTalepleriApproveVm
            {
                FinalName = detail.ProposedName,
                FinalSlug = detail.ProposedSlug
            },
            Reject = reject ?? new TenantTalepleriRejectVm()
        };
        ViewData["Title"] = $"Talep #{id} — Detay";
        return View("Detay", vm);
    }
}
