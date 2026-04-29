using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Models.TenantTalep;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Authorize]
[Route("tenant-talep")]
public sealed class TenantTalepController : Controller
{
    private readonly ITenantRequestService _requests;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantTalepController(ITenantRequestService requests, UserManager<ApplicationUser> userManager)
    {
        _requests = requests;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var active = await _requests.GetActiveRequestForUserAsync(user.Id, ct);
        var history = await _requests.GetUserRequestHistoryAsync(user.Id, ct);

        var vm = new TenantTalepDurumVm
        {
            ActiveRequest = active,
            History = history,
            UserAlreadyInTenant = user.TenantId.HasValue
        };

        ViewData["Title"] = "Hukuk Bürosu Talebim";
        ViewData["NoIndex"] = true;
        return View(vm);
    }

    [HttpGet("yeni")]
    public async Task<IActionResult> Yeni(CancellationToken ct = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (user.TenantId.HasValue)
        {
            TempData["Error"] = "Zaten bir tenant'a bağlısınız.";
            return RedirectToAction(nameof(Index));
        }

        var active = await _requests.GetActiveRequestForUserAsync(user.Id, ct);
        if (active != null)
        {
            TempData["Error"] = "Bekleyen bir talebiniz var.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Yeni Hukuk Bürosu Talebi";
        ViewData["NoIndex"] = true;
        return View(new TenantTalepCreateVm());
    }

    [HttpPost("yeni")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Yeni(TenantTalepCreateVm vm, CancellationToken ct = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            await _requests.CreateRequestAsync(user.Id,
                new CreateTenantRequestInput(vm.ProposedName, vm.ProposedSlug, vm.BarSicilNo, vm.Description), ct);
            TempData["Success"] = "✓ Talebiniz alındı. Admin onayını bekleyebilirsiniz.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(vm);
        }
    }

    [HttpPost("{id:int}/iptal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iptal(int id, CancellationToken ct = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await _requests.CancelRequestAsync(id, user.Id, ct);
            TempData["Success"] = "✓ Talebiniz iptal edildi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
