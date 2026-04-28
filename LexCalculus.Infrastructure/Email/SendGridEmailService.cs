using LexCalculus.Core.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace LexCalculus.Infrastructure.Email;

public sealed class SendGridEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(IOptions<EmailOptions> options, ILogger<SendGridEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SendGrid.ApiKey))
        {
            _logger.LogError("SendGrid API key tanımsız — User Secrets veya appsettings'e ekleyin.");
            return false;
        }

        try
        {
            var client = new SendGridClient(_options.SendGrid.ApiKey);
            var from = new EmailAddress(_options.From, _options.FromDisplayName);
            var to = new EmailAddress(message.ToAddress, message.ToDisplayName);
            var msg = MailHelper.CreateSingleEmail(
                from, to, message.Subject,
                message.PlainTextBody ?? "", message.HtmlBody);

            var response = await client.SendEmailAsync(msg, ct);
            var success = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;

            if (success)
            {
                _logger.LogInformation("SendGrid e-posta gönderildi: To={To} Subject='{Subject}' Status={Status}",
                    message.ToAddress, message.Subject, response.StatusCode);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync(ct);
                _logger.LogError("SendGrid hata: To={To} Status={Status} Body={Body}",
                    message.ToAddress, response.StatusCode, body);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid e-posta gönderim hatası: To={To}", message.ToAddress);
            return false;
        }
    }
}
