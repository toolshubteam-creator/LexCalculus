namespace LexCalculus.Core.Email;

public sealed record EmailMessage(
    string ToAddress,
    string? ToDisplayName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);

/// <summary>
/// Soyut e-posta gönderim servisi. 3 implementasyon:
/// - LoggingEmailService: dev/test, Serilog'a yazar, gerçek mail göndermez
/// - SmtpEmailService: staging veya self-hosted SMTP server için
/// - SendGridEmailService: production
/// Aktif provider appsettings.json "Email:Provider" ile seçilir.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Gönderim başarılıysa true. Hata durumunda false (logger'a yazar, throw etmez).
    /// Background job'larda transient failure'ları silent retry'a bırakırız.
    /// </summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);
}
