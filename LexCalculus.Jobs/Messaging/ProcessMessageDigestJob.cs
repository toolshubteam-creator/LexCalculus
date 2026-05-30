using Hangfire;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Email;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Jobs.Messaging;

/// <summary>
/// Okunmamış mesaj dijesti job'ı (Faz 6.2 P2, charter §3 Karar 2). Her dakika
/// çalışır; bir kullanıcının EN ESKİ gönderilmemiş dijest kalemi 5 dk eşiğini
/// geçmişse, o kullanıcının TÜM gönderilmemiş kalemlerini tek e-postada toplar
/// (dijest semantiği — 5 dk + 30 sn ayrı ayrı gitmez). Master + granüler tercih
/// + anonimize burada da kontrol edilir (defense-in-depth).
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class ProcessMessageDigestJob
{
    private static readonly TimeSpan DigestDelay = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _ctx;
    private readonly IEmailService _email;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly string _siteUrl;
    private readonly ILogger<ProcessMessageDigestJob> _logger;

    public ProcessMessageDigestJob(
        ApplicationDbContext ctx,
        IEmailService email,
        IEmailTemplateRenderer renderer,
        IConfiguration config,
        ILogger<ProcessMessageDigestJob> logger)
    {
        _ctx = ctx;
        _email = email;
        _renderer = renderer;
        _siteUrl = (config["SeoSettings:SiteUrl"] ?? "https://lexcalculus.com").TrimEnd('/');
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var threshold = DateTime.UtcNow - DigestDelay;

        // Eşiği geçmiş en az bir kalemi olan kullanıcılar
        var readyUserIds = await _ctx.EmailDigestEntries
            .Where(e => !e.IsSent && e.Type == EmailDigestType.Message && e.CreatedAt <= threshold)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in readyUserIds)
        {
            try
            {
                await ProcessUserAsync(userId, ct);
            }
            catch (Exception ex)
            {
                // Bu kullanıcının kalemleri IsSent=false kalır, sonraki run'da tekrar denenir.
                _logger.LogError(ex, "Mesaj dijesti gönderilemedi: user={UserId}", userId);
            }
        }
    }

    private async Task ProcessUserAsync(int userId, CancellationToken ct)
    {
        var user = await _ctx.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        // Kullanıcının TÜM gönderilmemiş mesaj dijesti kalemleri (eşik sonrası dahil)
        var entries = await _ctx.EmailDigestEntries
            .Where(e => e.UserId == userId && !e.IsSent && e.Type == EmailDigestType.Message)
            .ToListAsync(ct);
        if (entries.Count == 0) return;

        var canSend = user is { IsActive: true }
                      && !string.IsNullOrEmpty(user.Email)
                      && user.NotificationsEmailEnabled
                      && user.Profile is { EmailOnMessageDigest: true };

        if (!canSend)
        {
            // Tercih kapalı / kullanıcı pasif → kalemleri "işlendi" say, kuyruğu şişirme.
            MarkSent(entries);
            await _ctx.SaveChangesAsync(ct);
            return;
        }

        var messageIds = entries
            .Where(e => e.RelatedEntityId.HasValue)
            .Select(e => e.RelatedEntityId!.Value)
            .ToList();

        var senderRows = await _ctx.Messages
            .Where(m => messageIds.Contains(m.Id))
            .Select(m => new
            {
                m.Sender!.UserName,
                ProfileName = m.Sender.Profile != null ? m.Sender.Profile.DisplayName : null
            })
            .ToListAsync(ct);

        var distinctSenders = senderRows
            .Select(x => string.IsNullOrEmpty(x.ProfileName) ? (x.UserName ?? "Bilinmeyen") : x.ProfileName!)
            .Distinct()
            .ToList();

        var recipientName = string.IsNullOrEmpty(user!.Profile?.DisplayName)
            ? (user.UserName ?? "")
            : user.Profile!.DisplayName;

        var model = new MessageDigestEmailModel
        {
            RecipientDisplayName = recipientName,
            UnreadCount = entries.Count,
            SenderDisplayNames = distinctSenders,
            MessagesUrl = $"{_siteUrl}/mesajlar"
        };

        var html = await _renderer.RenderAsync("MessageDigest", model, ct);
        var sent = await _email.SendAsync(
            new EmailMessage(user.Email!, recipientName, $"{entries.Count} yeni mesajınız var", html), ct);

        if (sent)
        {
            MarkSent(entries);
            await _ctx.SaveChangesAsync(ct);
        }
    }

    private static void MarkSent(List<EmailDigestEntry> entries)
    {
        var now = DateTime.UtcNow;
        foreach (var e in entries)
        {
            e.IsSent = true;
            e.SentAt = now;
        }
    }
}
