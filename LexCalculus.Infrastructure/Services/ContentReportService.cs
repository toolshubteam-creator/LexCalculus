using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Moderation;
using LexCalculus.Core.Notifications;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// ContentReportService — kullanıcı şikayetleri + admin moderasyon iskeleti.
/// CreateAsync kullanılıyor (Faz 4.10 P1); Dismiss/Action P2'de admin UI'dan
/// çağrılacak. ActivityLog: ContentReport.Create (P2'de Dismiss + Action eklenecek).
/// </summary>
public sealed class ContentReportService : IContentReportService
{
    private const int MinOtherNoteLength = 10;

    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<ContentReportService>? _logger;

    public ContentReportService(
        ApplicationDbContext ctx,
        INotificationService notifications,
        IActivityLogService activityLog,
        ILogger<ContentReportService>? logger = null)
    {
        _ctx = ctx;
        _notifications = notifications;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<ContentReportResult> CreateAsync(
        ContentReportTargetType targetType,
        int targetId,
        int reporterId,
        ContentReportReason reason,
        string? note,
        CancellationToken ct = default)
    {
        if (targetId <= 0)
            return new ContentReportResult(false, "Geçersiz hedef.", null);
        if (reporterId <= 0)
            return new ContentReportResult(false, "Geçersiz kullanıcı.", null);

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is not null && trimmedNote.Length > 500)
            return new ContentReportResult(false, "Açıklama 500 karakteri aşamaz.", null);

        if (reason == ContentReportReason.Other)
        {
            if (string.IsNullOrWhiteSpace(trimmedNote) || trimmedNote.Length < MinOtherNoteLength)
                return new ContentReportResult(false,
                    $"\"Diğer\" sebebinde en az {MinOtherNoteLength} karakter açıklama girin.", null);
        }

        // Hedef varlık doğrulama + self-report engeli
        int targetOwnerId;
        switch (targetType)
        {
            case ContentReportTargetType.Post:
            {
                var post = await _ctx.UserPosts
                    .Where(p => p.Id == targetId)
                    .Select(p => new { p.UserId, p.IsPublished })
                    .FirstOrDefaultAsync(ct);
                if (post is null)
                    return new ContentReportResult(false, "Şikayet edilecek içerik bulunamadı.", null);
                if (!post.IsPublished)
                    return new ContentReportResult(false, "Yayında olmayan içerik şikayet edilemez.", null);
                targetOwnerId = post.UserId;
                break;
            }
            case ContentReportTargetType.Comment:
            {
                var comment = await _ctx.PostComments
                    .Where(c => c.Id == targetId)
                    .Select(c => new { c.UserId })
                    .FirstOrDefaultAsync(ct);
                if (comment is null)
                    return new ContentReportResult(false, "Şikayet edilecek yorum bulunamadı.", null);
                targetOwnerId = comment.UserId;
                break;
            }
            default:
                return new ContentReportResult(false, "Geçersiz hedef tipi.", null);
        }

        if (targetOwnerId == reporterId)
            return new ContentReportResult(false, "Kendi içeriğinizi şikayet edemezsiniz.", null);

        // Mükerrer kontrol (servis seviyesinde; DB unique index defansif)
        var alreadyReported = await _ctx.ContentReports.AnyAsync(r =>
            r.ReporterId == reporterId
            && r.TargetType == targetType
            && r.TargetId == targetId, ct);
        if (alreadyReported)
            return new ContentReportResult(false, "Bu içeriği zaten şikayet ettiniz.", null);

        var report = new ContentReport
        {
            TargetType = targetType,
            TargetId = targetId,
            ReporterId = reporterId,
            Reason = reason,
            Note = trimmedNote,
            Status = ContentReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.ContentReports.Add(report);

        try
        {
            await _ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Yarış koşulu (race) — paralel iki istek aynı anda
            _logger?.LogWarning(ex,
                "ContentReport DbUpdateException — muhtemelen race ile mükerrer girişim.");
            return new ContentReportResult(false, "Bu içeriği zaten şikayet ettiniz.", null);
        }

        await _activityLog.LogAsync(
            action: "ContentReport.Create",
            entityType: nameof(ContentReport),
            entityId: report.Id,
            description: $"Şikayet oluşturuldu: targetType={targetType} targetId={targetId} reason={reason}",
            metadata: new
            {
                ReportId = report.Id,
                TargetType = targetType.ToString(),
                TargetId = targetId,
                ReporterId = reporterId,
                Reason = reason.ToString()
            },
            ct: ct);

        return new ContentReportResult(true, null, report);
    }

    public async Task<IReadOnlyList<ContentReportGroup>> GetPendingGroupedAsync(
        CancellationToken ct = default)
    {
        var raw = await _ctx.ContentReports
            .Where(r => r.Status == ContentReportStatus.Pending)
            .GroupBy(r => new { r.TargetType, r.TargetId })
            .Select(g => new
            {
                g.Key.TargetType,
                g.Key.TargetId,
                ReportCount = g.Count(),
                LatestReportAt = g.Max(r => r.CreatedAt)
            })
            .OrderByDescending(g => g.LatestReportAt)
            .ToListAsync(ct);

        if (raw.Count == 0)
            return Array.Empty<ContentReportGroup>();

        // Hedef title + author display name doldurma (Post + Comment ayrı queries)
        var postIds = raw.Where(r => r.TargetType == ContentReportTargetType.Post)
            .Select(r => r.TargetId).ToList();
        var commentIds = raw.Where(r => r.TargetType == ContentReportTargetType.Comment)
            .Select(r => r.TargetId).ToList();

        var posts = new Dictionary<int, (string? Title, string? Author)>();
        if (postIds.Count > 0)
        {
            var postRows = await _ctx.UserPosts
                .Where(p => postIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    AuthorName = p.User!.Profile != null
                        ? p.User.Profile.DisplayName
                        : p.User.UserName
                })
                .ToListAsync(ct);
            foreach (var r in postRows)
                posts[r.Id] = ((string?)r.Title, (string?)r.AuthorName);
        }

        var comments = new Dictionary<int, (string? Title, string? Author)>();
        if (commentIds.Count > 0)
        {
            var commentRows = await _ctx.PostComments
                .Where(c => commentIds.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    Body = c.Body,
                    AuthorName = c.User!.Profile != null
                        ? c.User.Profile.DisplayName
                        : c.User.UserName
                })
                .ToListAsync(ct);
            foreach (var r in commentRows)
                comments[r.Id] = (Truncate(r.Body, 80), (string?)r.AuthorName);
        }

        var result = new List<ContentReportGroup>(raw.Count);
        foreach (var g in raw)
        {
            string? title = null;
            string? author = null;
            if (g.TargetType == ContentReportTargetType.Post && posts.TryGetValue(g.TargetId, out var pv))
            { title = pv.Title; author = pv.Author; }
            else if (g.TargetType == ContentReportTargetType.Comment && comments.TryGetValue(g.TargetId, out var cv))
            { title = cv.Title; author = cv.Author; }

            result.Add(new ContentReportGroup(
                g.TargetType, g.TargetId, g.ReportCount, g.LatestReportAt, title, author));
        }
        return result;
    }

    public Task<ContentReport?> GetByIdAsync(int id, CancellationToken ct = default)
        => _ctx.ContentReports
            .Include(r => r.Reporter)
            .Include(r => r.ReviewedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ContentReport>> GetByTargetAsync(
        ContentReportTargetType targetType, int targetId, CancellationToken ct = default)
        => await _ctx.ContentReports
            .Include(r => r.Reporter)
            .Include(r => r.ReviewedBy)
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> HasReportedAsync(
        ContentReportTargetType targetType, int targetId, int userId, CancellationToken ct = default)
        => _ctx.ContentReports.AnyAsync(r =>
            r.TargetType == targetType
            && r.TargetId == targetId
            && r.ReporterId == userId, ct);

    public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => _ctx.ContentReports.CountAsync(r => r.Status == ContentReportStatus.Pending, ct);

    public async Task<ContentReportResult> HideAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, string? reviewNote, CancellationToken ct = default)
    {
        int? contentOwnerId = null;
        bool wasAlreadyHidden = false;
        switch (targetType)
        {
            case ContentReportTargetType.Post:
            {
                var post = await _ctx.UserPosts.FirstOrDefaultAsync(p => p.Id == targetId, ct);
                if (post is null)
                    return new ContentReportResult(false, "İçerik bulunamadı.", null);
                wasAlreadyHidden = post.IsModeratorHidden;
                contentOwnerId = post.UserId;
                post.IsModeratorHidden = true;
                post.UpdatedAt = DateTime.UtcNow;
                break;
            }
            case ContentReportTargetType.Comment:
            {
                var comment = await _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == targetId, ct);
                if (comment is null)
                    return new ContentReportResult(false, "İçerik bulunamadı.", null);
                wasAlreadyHidden = comment.IsModeratorHidden;
                contentOwnerId = comment.UserId;
                comment.IsModeratorHidden = true;
                comment.UpdatedAt = DateTime.UtcNow;
                break;
            }
            default:
                return new ContentReportResult(false, "Geçersiz hedef tipi.", null);
        }

        // Pending raporları Actioned'a çek (Hide de aksiyon)
        var pending = await _ctx.ContentReports
            .Where(r => r.TargetType == targetType
                     && r.TargetId == targetId
                     && r.Status == ContentReportStatus.Pending)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var trimmedNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        if (trimmedNote is not null && trimmedNote.Length > 500)
            trimmedNote = trimmedNote.Substring(0, 500);

        foreach (var r in pending)
        {
            r.Status = ContentReportStatus.Actioned;
            r.ReviewedByUserId = adminUserId;
            r.ReviewedAt = now;
            r.ReviewNote = trimmedNote;
        }

        await _ctx.SaveChangesAsync(ct);

        // Notification (idempotent: zaten gizliyse de notify değil — sadece yeni hide)
        if (!wasAlreadyHidden)
        {
            // Reporter'lara "şikayetiniz değerlendirildi, içerik gizlendi"
            var reporterIds = pending.Select(r => r.ReporterId).Distinct().ToList();
            foreach (var reporterId in reporterIds)
            {
                await SafeNotifyAsync(() => _notifications.CreateAsync(
                    type: NotificationType.ContentHidden,
                    userId: reporterId,
                    title: "Şikayetiniz incelendi",
                    body: "Bildirdiğiniz içerik moderasyon kuralları gereği gizlendi.",
                    link: "/bildirimler",
                    relatedEntityType: nameof(ContentReport),
                    relatedEntityId: pending.First(r => r.ReporterId == reporterId).Id,
                    ct: ct));
            }

            // Content owner'a
            if (contentOwnerId.HasValue)
            {
                await SafeNotifyAsync(() => _notifications.CreateAsync(
                    type: NotificationType.ContentHidden,
                    userId: contentOwnerId.Value,
                    title: "İçeriğiniz gizlendi",
                    body: targetType == ContentReportTargetType.Post
                        ? "Makaleniz şikayet üzerine yönetim tarafından gizlendi."
                        : "Yorumunuz şikayet üzerine yönetim tarafından gizlendi.",
                    link: "/bildirimler",
                    relatedEntityType: targetType.ToString(),
                    relatedEntityId: targetId,
                    ct: ct));
            }
        }

        await _activityLog.LogAsync(
            action: "ContentReport.Hide",
            entityType: nameof(ContentReport),
            entityId: pending.Count > 0 ? pending[0].Id : 0,
            description: $"İçerik gizlendi: targetType={targetType} targetId={targetId}",
            metadata: new
            {
                TargetType = targetType.ToString(),
                TargetId = targetId,
                AdminUserId = adminUserId,
                ResolvedReportCount = pending.Count,
                WasAlreadyHidden = wasAlreadyHidden
            },
            ct: ct);

        return new ContentReportResult(true, null, pending.Count > 0 ? pending[0] : null);
    }

    public async Task<ContentReportResult> UnhideAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, CancellationToken ct = default)
    {
        int? contentOwnerId = null;
        switch (targetType)
        {
            case ContentReportTargetType.Post:
            {
                var post = await _ctx.UserPosts.FirstOrDefaultAsync(p => p.Id == targetId, ct);
                if (post is null)
                    return new ContentReportResult(false, "İçerik bulunamadı.", null);
                if (!post.IsModeratorHidden)
                    return new ContentReportResult(false, "İçerik zaten gizli değil.", null);
                contentOwnerId = post.UserId;
                post.IsModeratorHidden = false;
                post.UpdatedAt = DateTime.UtcNow;
                break;
            }
            case ContentReportTargetType.Comment:
            {
                var comment = await _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == targetId, ct);
                if (comment is null)
                    return new ContentReportResult(false, "İçerik bulunamadı.", null);
                if (!comment.IsModeratorHidden)
                    return new ContentReportResult(false, "İçerik zaten gizli değil.", null);
                contentOwnerId = comment.UserId;
                comment.IsModeratorHidden = false;
                comment.UpdatedAt = DateTime.UtcNow;
                break;
            }
            default:
                return new ContentReportResult(false, "Geçersiz hedef tipi.", null);
        }

        await _ctx.SaveChangesAsync(ct);

        if (contentOwnerId.HasValue)
        {
            await SafeNotifyAsync(() => _notifications.CreateAsync(
                type: NotificationType.ContentRestored,
                userId: contentOwnerId.Value,
                title: "İçeriğiniz geri yüklendi",
                body: targetType == ContentReportTargetType.Post
                    ? "Gizlenmiş makaleniz tekrar yayında."
                    : "Gizlenmiş yorumunuz tekrar görünür.",
                link: "/bildirimler",
                relatedEntityType: targetType.ToString(),
                relatedEntityId: targetId,
                ct: ct));
        }

        await _activityLog.LogAsync(
            action: "ContentReport.Unhide",
            entityType: targetType.ToString(),
            entityId: targetId,
            description: $"İçerik geri yüklendi: targetType={targetType} targetId={targetId}",
            metadata: new
            {
                TargetType = targetType.ToString(),
                TargetId = targetId,
                AdminUserId = adminUserId
            },
            ct: ct);

        return new ContentReportResult(true, null, null);
    }

    public async Task<IReadOnlyList<HiddenContentItem>> GetHiddenContentAsync(
        CancellationToken ct = default)
    {
        var items = new List<HiddenContentItem>();

        var hiddenPosts = await _ctx.UserPosts
            .AsNoTracking()
            .Include(p => p.User).ThenInclude(u => u!.Profile)
            .Where(p => p.IsModeratorHidden)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.UpdatedAt,
                AuthorName = p.User!.Profile != null
                    ? p.User.Profile.DisplayName
                    : p.User.UserName,
                AuthorSlug = p.User.Profile != null ? p.User.Profile.PublicSlug : null,
                AuthorActive = p.User.IsActive
            })
            .ToListAsync(ct);

        foreach (var p in hiddenPosts)
        {
            string? url = null;
            if (p.AuthorActive && !string.IsNullOrEmpty(p.AuthorSlug))
                url = $"/uye/{p.AuthorSlug}/makale/{p.Slug}";
            items.Add(new HiddenContentItem(
                ContentReportTargetType.Post, p.Id,
                p.Title ?? "(başlık yok)",
                p.AuthorName ?? "(yazar bulunamadı)",
                p.UpdatedAt, url));
        }

        var hiddenComments = await _ctx.PostComments
            .AsNoTracking()
            .Include(c => c.User).ThenInclude(u => u!.Profile)
            .Include(c => c.Post).ThenInclude(p => p.User).ThenInclude(u => u!.Profile)
            .Where(c => c.IsModeratorHidden)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new
            {
                c.Id,
                c.Body,
                c.UpdatedAt,
                AuthorName = c.User!.Profile != null
                    ? c.User.Profile.DisplayName
                    : c.User.UserName,
                PostSlug = c.Post.Slug,
                PostAuthorSlug = c.Post.User!.Profile != null ? c.Post.User.Profile.PublicSlug : null,
                PostAuthorActive = c.Post.User.IsActive
            })
            .ToListAsync(ct);

        foreach (var c in hiddenComments)
        {
            string? url = null;
            if (c.PostAuthorActive && !string.IsNullOrEmpty(c.PostAuthorSlug))
                url = $"/uye/{c.PostAuthorSlug}/makale/{c.PostSlug}#yorum-{c.Id}";
            var preview = c.Body.Length > 50
                ? "Yorum: " + c.Body.Substring(0, 50) + "..."
                : "Yorum: " + c.Body;
            items.Add(new HiddenContentItem(
                ContentReportTargetType.Comment, c.Id,
                preview,
                c.AuthorName ?? "(yazar bulunamadı)",
                c.UpdatedAt, url));
        }

        return items
            .OrderByDescending(i => i.UpdatedAt)
            .ToList();
    }

    public async Task<ContentReportResult> DismissAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, string? reviewNote, CancellationToken ct = default)
    {
        var pending = await _ctx.ContentReports
            .Where(r => r.TargetType == targetType
                     && r.TargetId == targetId
                     && r.Status == ContentReportStatus.Pending)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return new ContentReportResult(false, "İşlenecek bekleyen şikayet bulunamadı.", null);

        var now = DateTime.UtcNow;
        var trimmedNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        if (trimmedNote is not null && trimmedNote.Length > 500)
            trimmedNote = trimmedNote.Substring(0, 500);

        foreach (var r in pending)
        {
            r.Status = ContentReportStatus.Dismissed;
            r.ReviewedByUserId = adminUserId;
            r.ReviewedAt = now;
            r.ReviewNote = trimmedNote;
        }
        await _ctx.SaveChangesAsync(ct);

        // Reporter'lara bildirim — best-effort
        var reporterIds = pending.Select(r => r.ReporterId).Distinct().ToList();
        foreach (var reporterId in reporterIds)
        {
            await SafeNotifyAsync(() => _notifications.CreateAsync(
                type: NotificationType.ContentReportResolved,
                userId: reporterId,
                title: "Şikayetiniz incelendi",
                body: "Bildirdiğiniz içerik incelendi; ihlal tespit edilmedi.",
                link: "/bildirimler",
                relatedEntityType: nameof(ContentReport),
                relatedEntityId: pending.First(r => r.ReporterId == reporterId).Id,
                ct: ct));
        }

        await _activityLog.LogAsync(
            action: "ContentReport.Dismiss",
            entityType: nameof(ContentReport),
            entityId: pending[0].Id,
            description: $"Şikayet reddedildi: targetType={targetType} targetId={targetId} count={pending.Count}",
            metadata: new
            {
                TargetType = targetType.ToString(),
                TargetId = targetId,
                AdminUserId = adminUserId,
                ReportCount = pending.Count
            },
            ct: ct);

        return new ContentReportResult(true, null, pending[0]);
    }

    public async Task<ContentReportResult> ActionAsync(
        ContentReportTargetType targetType, int targetId,
        int adminUserId, string? reviewNote, CancellationToken ct = default)
    {
        var pending = await _ctx.ContentReports
            .Where(r => r.TargetType == targetType
                     && r.TargetId == targetId
                     && r.Status == ContentReportStatus.Pending)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return new ContentReportResult(false, "İşlenecek bekleyen şikayet bulunamadı.", null);

        // Hedefi bul + sahibini al + sil
        int? contentOwnerId = null;
        switch (targetType)
        {
            case ContentReportTargetType.Post:
            {
                var post = await _ctx.UserPosts
                    .Include(p => p.TagLinks)
                    .FirstOrDefaultAsync(p => p.Id == targetId, ct);
                if (post is null)
                    return new ContentReportResult(false, "İçerik zaten kaldırılmış.", null);
                contentOwnerId = post.UserId;

                // Yayındaysa tag UsageCount'ları azalt (cascade UsageCount güncellemez).
                // UserPostService.DeleteAsync ile aynı pattern; admin context'te servis-arası
                // bağımlılık eklemekten kaçınmak için inline.
                if (post.IsPublished && post.TagLinks.Count > 0)
                {
                    var tagIds = post.TagLinks.Select(l => l.TagId).ToList();
                    var tags = await _ctx.PostTags
                        .Where(t => tagIds.Contains(t.Id)).ToListAsync(ct);
                    foreach (var tag in tags)
                    {
                        if (tag.UsageCount > 0) tag.UsageCount--;
                    }
                }

                _ctx.UserPosts.Remove(post);
                break;
            }
            case ContentReportTargetType.Comment:
            {
                var comment = await _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == targetId, ct);
                if (comment is null)
                    return new ContentReportResult(false, "İçerik zaten kaldırılmış.", null);
                contentOwnerId = comment.UserId;
                _ctx.PostComments.Remove(comment);
                break;
            }
        }

        var now = DateTime.UtcNow;
        var trimmedNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        if (trimmedNote is not null && trimmedNote.Length > 500)
            trimmedNote = trimmedNote.Substring(0, 500);

        foreach (var r in pending)
        {
            r.Status = ContentReportStatus.Actioned;
            r.ReviewedByUserId = adminUserId;
            r.ReviewedAt = now;
            r.ReviewNote = trimmedNote;
        }
        await _ctx.SaveChangesAsync(ct);

        // Reporter'lara: içerik kaldırıldı
        var reporterIds = pending.Select(r => r.ReporterId).Distinct().ToList();
        foreach (var reporterId in reporterIds)
        {
            await SafeNotifyAsync(() => _notifications.CreateAsync(
                type: NotificationType.ContentReportResolved,
                userId: reporterId,
                title: "Şikayetiniz incelendi",
                body: "Bildirdiğiniz içerik moderasyon kuralları gereği kaldırıldı.",
                link: "/bildirimler",
                relatedEntityType: nameof(ContentReport),
                relatedEntityId: pending.First(r => r.ReporterId == reporterId).Id,
                ct: ct));
        }

        // İçerik sahibine: içeriği kaldırıldı
        if (contentOwnerId.HasValue)
        {
            await SafeNotifyAsync(() => _notifications.CreateAsync(
                type: NotificationType.ContentRemoved,
                userId: contentOwnerId.Value,
                title: "İçeriğiniz kaldırıldı",
                body: targetType == ContentReportTargetType.Post
                    ? "Makaleniz şikayet üzerine kaldırıldı."
                    : "Yorumunuz şikayet üzerine kaldırıldı.",
                link: "/bildirimler",
                relatedEntityType: nameof(ContentReport),
                relatedEntityId: pending[0].Id,
                ct: ct));
        }

        await _activityLog.LogAsync(
            action: "ContentReport.Action",
            entityType: nameof(ContentReport),
            entityId: pending[0].Id,
            description: $"Şikayet aksiyon: targetType={targetType} targetId={targetId} count={pending.Count}",
            metadata: new
            {
                TargetType = targetType.ToString(),
                TargetId = targetId,
                AdminUserId = adminUserId,
                ReportCount = pending.Count,
                ContentOwnerId = contentOwnerId
            },
            ct: ct);

        return new ContentReportResult(true, null, pending[0]);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private static string? Truncate(string? s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= maxLen) return s;
        return s.Substring(0, maxLen) + "…";
    }

    private async Task SafeNotifyAsync(Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "ContentReportService notification failed (asıl işlem etkilenmedi).");
        }
    }
}
