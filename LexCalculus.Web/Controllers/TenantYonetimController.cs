using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Models.TenantYonetim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Controllers;

[Authorize]
[Route("tenant/yonet")]
public sealed class TenantYonetimController : Controller
{
    private readonly ITenantAdminService _tenants;
    private readonly ITenantInvitationService _invitations;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantYonetimController(
        ITenantAdminService tenants,
        ITenantInvitationService invitations,
        UserManager<ApplicationUser> userManager)
    {
        _tenants = tenants;
        _invitations = invitations;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var (user, tenant, error) = await ResolveOwnerContextAsync(ct);
        if (error != null) return error;

        var invitations = await _invitations.GetForTenantAsync(tenant!.Id, ct);
        var vm = new TenantYonetimVm
        {
            Tenant = tenant,
            Invitations = invitations
        };

        ViewData["Title"] = "Tenant Yönetimi";
        ViewData["NoIndex"] = true;
        return View(vm);
    }

    [HttpPost("davet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Davet(TenantYonetimDavetVm vm, CancellationToken ct = default)
    {
        var (user, tenant, error) = await ResolveOwnerContextAsync(ct);
        if (error != null) return error;

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "E-posta geçerli formatta olmalı.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _invitations.CreateAsync(tenant!.Id, user!.Id, vm.Email, requesterIsAdmin: false, ct);
            TempData["Success"] = $"✓ Davet gönderildi: {vm.Email}";
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("davet/{id:int}/iptal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DavetIptal(int id, CancellationToken ct = default)
    {
        var (user, _, error) = await ResolveOwnerContextAsync(ct);
        if (error != null) return error;

        try
        {
            await _invitations.CancelAsync(id, user!.Id, isAdmin: false, ct);
            TempData["Success"] = "✓ Davet iptal edildi.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("uye/{id:int}/cikar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UyeCikar(int id, CancellationToken ct = default)
    {
        var (user, tenant, error) = await ResolveOwnerContextAsync(ct);
        if (error != null) return error;

        try
        {
            await _tenants.RemoveMemberAsync(tenant!.Id, id, ct);
            TempData["Success"] = "✓ Üye çıkarıldı.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<(ApplicationUser? user, TenantDetailDto? tenant, IActionResult? error)>
        ResolveOwnerContextAsync(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return (null, null, Challenge());

        if (!user.TenantId.HasValue)
        {
            TempData["Error"] = "Bir tenant'a bağlı değilsiniz.";
            return (user, null, Redirect("/"));
        }

        var tenant = await _tenants.GetByIdAsync(user.TenantId.Value, ct);
        if (tenant == null)
        {
            TempData["Error"] = "Bağlı olduğunuz tenant bulunamadı.";
            return (user, null, Redirect("/"));
        }

        if (tenant.OwnerUserId != user.Id)
        {
            TempData["Error"] = "Bu sayfaya yalnızca tenant owner erişebilir.";
            return (user, tenant, Redirect("/"));
        }

        return (user, tenant, null);
    }
}
