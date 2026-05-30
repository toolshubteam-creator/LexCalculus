using LexCalculus.Core.Email;
using LexCalculus.Core.Entities.Notifications;
using LexCalculus.Core.Notifications;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationEmailDispatcher? _emailDispatcher;

    public NotificationService(
        ApplicationDbContext ctx,
        ILogger<NotificationService> logger,
        INotificationEmailDispatcher? emailDispatcher = null)
    {
        _ctx = ctx;
        _logger = logger;
        _emailDispatcher = emailDispatcher;
    }

    /// <summary>
    /// Faz 6.2 P2 — yalnızca sosyal bildirim tipleri e-posta tetikler. Sistem
    /// tipleri (DataFreshness, ParameterChange, SystemAlert) DataFreshnessCheckJob'un
    /// kendi doğrudan-email akışını kullanır; burada tetiklenmez. NewMessage dijest
    /// akışındadır (MessageService → EmailDigestEntry).
    /// </summary>
    private static bool IsSocialEmailType(NotificationType type) => type switch
    {
        NotificationType.ConnectionRequest => true,
        NotificationType.ConnectionAccepted => true,
        NotificationType.PostComment => true,
        NotificationType.ContentHidden => true,
        NotificationType.ContentRestored => true,
        NotificationType.ContentRemoved => true,
        NotificationType.ContentReportResolved => true,
        _ => false
    };

    public async Task<Notification?> CreateAsync(
        NotificationType type,
        int userId,
        string title,
        string body,
        string? link = null,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        string? iconHint = null,
        TimeSpan? dedupWindow = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title boş olamaz.", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body boş olamaz.", nameof(body));

        if (dedupWindow.HasValue && relatedEntityType != null && relatedEntityId.HasValue)
        {
            var threshold = DateTime.UtcNow - dedupWindow.Value;
            var existing = await _ctx.Notifications
                .AnyAsync(n =>
                    n.UserId == userId &&
                    n.Type == type &&
                    n.RelatedEntityType == relatedEntityType &&
                    n.RelatedEntityId == relatedEntityId &&
                    n.CreatedAt > threshold, ct);

            if (existing)
            {
                _logger.LogDebug(
                    "Notification dedup hit: UserId={UserId} Type={Type} EntityType={EntityType} EntityId={EntityId}",
                    userId, type, relatedEntityType, relatedEntityId);
                return null;
            }
        }

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Body = body.Trim(),
            Link = link,
            IconHint = iconHint,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.Notifications.Add(notification);
        await _ctx.SaveChangesAsync(ct);

        // Faz 6.2 P2 — sosyal tip ise e-posta tetikle (best-effort, akışı bozmaz).
        if (_emailDispatcher is not null && IsSocialEmailType(type))
        {
            try
            {
                await _emailDispatcher.DispatchAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Bildirim e-postası dispatch hatası (notification kaydedildi): {Id}",
                    notification.Id);
            }
        }

        return notification;
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        _ctx.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId, int limit, bool unreadOnly, CancellationToken ct = default)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));

        var q = _ctx.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly)
            q = q.Where(n => !n.IsRead);

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task MarkAsReadAsync(int notificationId, int userId, CancellationToken ct = default)
    {
        var n = await _ctx.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);

        if (n is null)
            throw new InvalidOperationException(
                $"Notification {notificationId} not found or does not belong to user {userId}.");

        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        var unread = await _ctx.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _ctx.SaveChangesAsync(ct);
        return unread.Count;
    }

    public Task<int> GetTotalActiveCountAsync(CancellationToken ct = default) =>
        _ctx.Notifications.CountAsync(ct);
}
