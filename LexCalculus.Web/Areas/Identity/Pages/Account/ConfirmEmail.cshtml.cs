#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _emailRenderer;
    private readonly ILogger<ConfirmEmailModel> _logger;

    public ConfirmEmailModel(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IEmailTemplateRenderer emailRenderer,
        ILogger<ConfirmEmailModel> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _logger = logger;
    }

    public bool IsSuccess { get; private set; }
    public string ErrorMessage { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            IsSuccess = false;
            ErrorMessage = "Bağlantı geçersiz: parametre eksik.";
            return Page();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            IsSuccess = false;
            ErrorMessage = "Bağlantı geçersiz: kullanıcı bulunamadı.";
            return Page();
        }

        if (user.EmailConfirmed)
        {
            IsSuccess = true;
            return Page();
        }

        try
        {
            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);

            if (!result.Succeeded)
            {
                IsSuccess = false;
                ErrorMessage = "Bağlantı geçersiz veya süresi dolmuş. Lütfen yeniden kayıt olun.";
                _logger.LogWarning(
                    "ConfirmEmail failed for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return Page();
            }

            IsSuccess = true;
            _logger.LogInformation("E-posta doğrulandı: {Email}", user.Email);

            try
            {
                var siteUrl = $"{Request.Scheme}://{Request.Host}";
                var displayName = !string.IsNullOrWhiteSpace(user.FullName)
                    ? user.FullName
                    : user.Email ?? "Kullanıcı";

                var model = new WelcomeEmailModel
                {
                    DisplayName = displayName,
                    SiteUrl = siteUrl,
                    ProfileUrl = $"{siteUrl}/profil",
                    CalculatorsUrl = siteUrl
                };

                var html = await _emailRenderer.RenderAsync("WelcomeEmail", model);
                var emailMessage = new EmailMessage(
                    ToAddress: user.Email!,
                    ToDisplayName: displayName,
                    Subject: "Lex Calculus — Hoş Geldiniz",
                    HtmlBody: html);

                await _emailService.SendAsync(emailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Welcome email gönderilemedi: {Email}", user.Email);
                // Akışı bozma — doğrulama başarılı, hoş geldin maili opsiyonel
            }

            return Page();
        }
        catch (FormatException)
        {
            IsSuccess = false;
            ErrorMessage = "Bağlantı geçersiz: token format hatası.";
            return Page();
        }
    }
}
