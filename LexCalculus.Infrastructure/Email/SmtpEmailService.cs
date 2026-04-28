using System.Net;
using System.Net.Mail;
using LexCalculus.Core.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = string.IsNullOrEmpty(_options.Smtp.Username)
            };

            if (!string.IsNullOrEmpty(_options.Smtp.Username))
                client.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);

            using var mail = new MailMessage
            {
                From = new MailAddress(_options.From, _options.FromDisplayName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            mail.To.Add(new MailAddress(message.ToAddress, message.ToDisplayName ?? ""));

            if (!string.IsNullOrEmpty(message.PlainTextBody))
            {
                var textView = AlternateView.CreateAlternateViewFromString(
                    message.PlainTextBody, null, "text/plain");
                mail.AlternateViews.Add(textView);
            }

            await client.SendMailAsync(mail, ct);
            _logger.LogInformation("SMTP e-posta gönderildi: To={To} Subject='{Subject}'",
                message.ToAddress, message.Subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP e-posta gönderim hatası: To={To} Subject='{Subject}'",
                message.ToAddress, message.Subject);
            return false;
        }
    }
}
