using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Jobs.Tenancy;

/// <summary>
/// Pending TenantInvitation kayıtlarından ExpiresAt &lt; UtcNow olanları
/// Status=Expired'e çeker. Günde bir kez (03:00 Europe/Istanbul) çalışır.
///
/// LookupByTokenAsync zaten request-time lazy expiration yapıyor; bu job
/// kullanılmamış davetlerin de zamanında temizlenmesini sağlar (filter,
/// metric ve raporlama için doğru durum).
///
/// Karar: hiç expired yoksa ActivityLog yazılmaz (gürültü engeli).
/// </summary>
public sealed class ExpireInvitationsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<ExpireInvitationsJob> _logger;

    public ExpireInvitationsJob(
        ApplicationDbContext ctx,
        IActivityLogService activityLog,
        ILogger<ExpireInvitationsJob> logger)
    {
        _ctx = ctx;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation("ExpireInvitationsJob başladı: {Time}", now);

        // Background job — TenantInvitation'da query filter yok, ama defansif AsAdminQuery.
        var expired = await _ctx.TenantInvitations
            .AsAdminQuery()
            .Where(i => i.Status == TenantInvitationStatus.Pending && i.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            _logger.LogInformation("ExpireInvitationsJob bitti: hiç expired davet yok.");
            return;
        }

        var ids = expired.Select(i => i.Id).ToList();

        foreach (var inv in expired)
            inv.Status = TenantInvitationStatus.Expired;

        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "System.ExpireInvitations",
            entityType: nameof(TenantInvitation),
            description: $"{expired.Count} davet süresi dolmuş, Expired'e çekildi",
            metadata: new { ExpiredCount = expired.Count, InvitationIds = ids },
            ct: ct);

        _logger.LogInformation("ExpireInvitationsJob bitti: {Count} davet expired.", expired.Count);
    }
}
