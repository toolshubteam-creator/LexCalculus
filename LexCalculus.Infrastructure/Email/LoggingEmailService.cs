using LexCalculus.Core.Email;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Email;

public sealed class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[LoggingEmailService] E-posta gönderildi (simüle): To={To} Subject='{Subject}' BodyLength={Len} chars",
            message.ToAddress,
            message.Subject,
            message.HtmlBody?.Length ?? 0);
        return Task.FromResult(true);
    }
}
