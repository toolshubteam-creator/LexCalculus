using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Web.Areas.Admin.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/kullanicilar")]
public sealed class UsersController : Controller
{
    private readonly IUserAdminService _users;
    private readonly IUserAnonymizationService _anonymization;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(
        IUserAdminService users,
        IUserAnonymizationService anonymization,
        UserManager<ApplicationUser> userManager)
    {
        _users = users;
        _anonymization = anonymization;
        _userManager = userManager;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        [FromQuery] UserListFilterViewModel? filter,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        filter ??= new UserListFilterViewModel();
        const int pageSize = 25;

        var result = await _users.GetUsersAsync(
            page, pageSize,
            roleFilter: string.IsNullOrWhiteSpace(filter.Role) ? null : filter.Role,
            isActiveFilter: filter.IsActive,
            ct: ct);

        var vm = new UserListPageViewModel
        {
            Result = result,
            Filter = filter
        };

        ViewData["Title"] = "Kullanıcılar";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kullanıcılar", null)
        };

        return View(vm);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
    {
        var detail = await _users.GetUserDetailAsync(id, ct);
        if (detail == null) return NotFound();

        ViewData["Title"] = $"{detail.Email} — Detay";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("Kullanıcılar", Url.Action("Index")),
            (detail.Email, null)
        };

        return View(detail);
    }

    [HttpPost("{id:int}/pasiflestir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pasiflestir(int id, CancellationToken ct = default)
    {
        if (IsCurrentUser(id))
        {
            TempData["AdminError"] = "Kendinizi pasifleştiremezsiniz.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Service tarafı owner protection (Faz 3.7 P2a/5) InvalidOperationException atar —
        // self-deactivate guard pattern'iyle uyumlu TempData mesajı.
        try
        {
            var ok = await _users.SetActiveAsync(id, active: false, ct);
            TempData[ok ? "AdminSuccess" : "AdminError"] = ok
                ? "✓ Kullanıcı pasifleştirildi. Aktif oturumları kapatıldı."
                : "Pasifleştirme başarısız.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["AdminError"] = ex.Message;
        }
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/aktiflestir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aktiflestir(int id, CancellationToken ct = default)
    {
        var ok = await _users.SetActiveAsync(id, active: true, ct);
        TempData[ok ? "AdminSuccess" : "AdminError"] = ok
            ? "✓ Kullanıcı aktifleştirildi."
            : "Aktifleştirme başarısız.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/rol-degistir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RolDegistir(int id, [FromForm] string yeniRol, CancellationToken ct = default)
    {
        if (IsCurrentUser(id) && yeniRol != "Admin")
        {
            TempData["AdminError"] = "Kendi Admin rolünüzü düşüremezsiniz.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var ok = await _users.ChangeRoleAsync(id, yeniRol, ct);
        TempData[ok ? "AdminSuccess" : "AdminError"] = ok
            ? $"✓ Kullanıcının rolü '{yeniRol}' olarak güncellendi. Aktif oturumları kapatıldı."
            : "Rol değişikliği başarısız.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/sifre-sifirla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SifreSifirla(int id, CancellationToken ct = default)
    {
        var siteUrl = $"{Request.Scheme}://{Request.Host}";
        var ok = await _users.SendPasswordResetEmailAsync(id, siteUrl, ct);
        TempData[ok ? "AdminSuccess" : "AdminError"] = ok
            ? "✓ Şifre sıfırlama maili gönderildi."
            : "Şifre sıfırlama maili gönderilemedi.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id:int}/anonimize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anonimize(int id, CancellationToken ct = default)
    {
        if (IsCurrentUser(id))
        {
            TempData["AdminError"] = "Kendi hesabınızı bu panelden anonimize edemezsiniz.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var rawAdmin = _userManager.GetUserId(User);
        if (!int.TryParse(rawAdmin, out var adminId))
            return Unauthorized();

        var result = await _anonymization.AnonymizeAsync(id, adminId, ct);
        TempData[result.Success ? "AdminSuccess" : "AdminError"] = result.Success
            ? "✓ Kullanıcı anonimize edildi. İçerikleri korundu, yazar 'Silinmiş Kullanıcı' olarak görünüyor."
            : (result.ErrorMessage ?? "Anonimize işlemi başarısız.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    private bool IsCurrentUser(int id)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out var current) && current == id;
    }
}
