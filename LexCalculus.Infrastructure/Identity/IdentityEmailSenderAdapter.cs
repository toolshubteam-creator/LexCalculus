using LexCalculus.Core.Email;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity'nin IEmailSender interface'ini bizim IEmailService
/// altyapısına bağlayan adapter. Identity API tarafından üretilen e-posta
/// confirmation, password reset gibi sistem e-postaları bu köprü üzerinden
/// gerçek provider'a (Logging/Smtp/SendGrid) ulaşır.
/// </summary>
public sealed class IdentityEmailSenderAdapter : IEmailSender
{
    private readonly IEmailService _emailService;
    private readonly ILogger<IdentityEmailSenderAdapter> _logger;

    public IdentityEmailSenderAdapter(
        IEmailService emailService,
        ILogger<IdentityEmailSenderAdapter> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("IdentityEmailSenderAdapter: boş e-posta adresi, gönderim atlandı.");
            return;
        }

        var message = new EmailMessage(
            ToAddress: email,
            ToDisplayName: null,
            Subject: subject,
            HtmlBody: htmlMessage);

        try
        {
            var sent = await _emailService.SendAsync(message);
            if (!sent)
            {
                _logger.LogWarning(
                    "IdentityEmailSenderAdapter: gönderim başarısız (provider false döndü). To={Email} Subject={Subject}",
                    email, subject);
            }
        }
        catch (Exception ex)
        {
            // Identity API'sinin Task döner — exception throw edersek
            // Register/ResetPassword flow'u patlar. Onun yerine log + sessizce devam.
            _logger.LogError(ex,
                "IdentityEmailSenderAdapter: gönderim hatası. To={Email} Subject={Subject}",
                email, subject);
        }
    }
}
