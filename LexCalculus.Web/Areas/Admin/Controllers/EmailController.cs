using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
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
            var html = await RenderSampleAsync(vm.TemplateName, vm.RecipientName ?? "Admin", ct);
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

    /// <summary>
    /// Seçilen şablon için örnek (hardcoded) model ile HTML render eder — admin
    /// smoke amaçlı. Gerçek gönderimde model'ler ilgili servislerce doldurulur.
    /// </summary>
    private Task<string> RenderSampleAsync(string templateName, string recipientName, CancellationToken ct) =>
        templateName switch
        {
            "Connection" => _renderer.RenderAsync("Connection", new ConnectionEmailModel
            {
                RecipientDisplayName = recipientName,
                OtherDisplayName = "Av. Selin Demir",
                IsAccepted = false,
                ProfileUrl = "https://lexcalculus.local/uye/selin-demir"
            }, ct),

            "Comment" => _renderer.RenderAsync("Comment", new CommentEmailModel
            {
                RecipientDisplayName = recipientName,
                CommenterDisplayName = "Av. Selin Demir",
                PostTitle = "Kıdem Tazminatı Hesaplama Rehberi",
                CommentBodyPreview = "Çok faydalı bir yazı olmuş, teşekkürler. Bir sorum olacaktı...",
                PostUrl = "https://lexcalculus.local/uye/ahmet/makale/kidem-tazminati"
            }, ct),

            "ContentReport" => _renderer.RenderAsync("ContentReport", new ContentReportEmailModel
            {
                RecipientDisplayName = recipientName,
                ActionType = "Gizlendi",
                ContentType = "Yorum",
                ContentTitle = null,
                ReviewNote = "Topluluk kuralları gereği gizlendi."
            }, ct),

            "MessageDigest" => _renderer.RenderAsync("MessageDigest", new MessageDigestEmailModel
            {
                RecipientDisplayName = recipientName,
                UnreadCount = 3,
                SenderDisplayNames = new[] { "Av. Selin Demir", "Bilirkişi Mehmet Kaya" },
                MessagesUrl = "https://lexcalculus.local/mesajlar"
            }, ct),

            _ => _renderer.RenderAsync("TestEmail", new TestEmailModel
            {
                RecipientName = recipientName,
                Provider = _options.Provider,
                SentAt = DateTime.UtcNow,
                MachineName = Environment.MachineName
            }, ct),
        };
}
