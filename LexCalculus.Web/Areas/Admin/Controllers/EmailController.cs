using LexCalculus.Core.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Web.Areas.Admin.Models;
using LexCalculus.Web.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LexCalculus.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
[Route("admin/email")]
public sealed class EmailController : Controller
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly EmailOptions _options;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmailController(
        IEmailService emailService,
        IEmailTemplateRenderer renderer,
        IOptions<EmailOptions> options,
        UserManager<ApplicationUser> userManager)
    {
        _emailService = emailService;
        _renderer = renderer;
        _options = options.Value;
        _userManager = userManager;
    }

    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var vm = new TestEmailFormViewModel
        {
            ToAddress = currentUser?.Email ?? "",
            RecipientName = currentUser?.UserName ?? "Admin",
            ActiveProvider = _options.Provider
        };

        ViewData["Title"] = "E-posta Testi";
        ViewData["Breadcrumb"] = new List<(string Label, string? Url)>
        {
            ("Dashboard", Url.Action("Index", "Home", new { area = "Admin" })),
            ("E-posta Testi", null)
        };

        return View(vm);
    }

    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(TestEmailFormViewModel vm, CancellationToken ct)
    {
        vm.ActiveProvider = _options.Provider;

        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            var model = new TestEmailModel
            {
                RecipientName = vm.RecipientName ?? "Admin",
                Provider = _options.Provider,
                SentAt = DateTime.UtcNow,
                MachineName = Environment.MachineName
            };

            var html = await _renderer.RenderAsync("TestEmail", model, ct);
            var success = await _emailService.SendAsync(
                new EmailMessage(vm.ToAddress, vm.RecipientName, vm.Subject, html), ct);

            if (success)
            {
                TempData["AdminSuccess"] =
                    $"Test e-posta '{vm.ToAddress}' adresine gönderildi (provider: {_options.Provider}). " +
                    "Logging modunda iseniz Logs/log-*.txt dosyasını kontrol edin.";
            }
            else
            {
                TempData["AdminError"] =
                    "E-posta gönderim başarısız. Server log'larında detay var.";
            }
        }
        catch (Exception ex)
        {
            TempData["AdminError"] = $"Hata: {ex.Message}";
        }

        return RedirectToAction(nameof(Test));
    }
}
