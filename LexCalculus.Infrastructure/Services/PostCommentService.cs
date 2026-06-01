using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Notifications;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class PostCommentService : IPostCommentService
{
    private const int MaxRawBodyLength = 1000;

    private readonly ApplicationDbContext _ctx;
    private readonly ICommentSanitizer _sanitizer;
    private readonly INotificationService _notifications;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<PostCommentService>? _logger;

    public PostCommentService(
        ApplicationDbContext ctx,
        ICommentSanitizer sanitizer,
        INotificationService notifications,
        IActivityLogService activityLog,
        ILogger<PostCommentService>? logger = null)
    {
        _ctx = ctx;
        _sanitizer = sanitizer;
        _notifications = notifications;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<PostCommentResult> CreateAsync(
        int postId, int userId, string rawBody, CancellationToken ct = default)
    {
        var validation = ValidateBody(rawBody);
        if (validation is not null) return validation;

        var post = await _ctx.UserPosts
            .Where(p => p.Id == postId)
            .Select(p => new { p.Id, p.UserId, p.IsPublished, p.Slug,
                AuthorSlug = p.User!.Profile != null ? p.User.Profile.PublicSlug : null })
            .FirstOrDefaultAsync(ct);

        if (post is null)
            return new PostCommentResult(false, "Makale bulunamadı.", null);
        if (!post.IsPublished)
            return new PostCommentResult(false, "Yayında olmayan makaleye yorum yapılamaz.", null);

        var processedBody = CommentBodyProcessor.Process(rawBody, _sanitizer);
        if (string.IsNullOrEmpty(processedBody))
            return new PostCommentResult(false, "Yorum içeriği boş olamaz.", null);

        // Sanitize sonrası 2000 char DB sınırı için defansif truncate
        if (processedBody.Length > 2000)
            processedBody = processedBody.Substring(0, 2000);

        var now = DateTime.UtcNow;
        var comment = new PostComment
        {
            PostId = postId,
            UserId = userId,
            Body = processedBody,
            CreatedAt = now,
            UpdatedAt = now,
            IsEdited = false
        };
        _ctx.PostComments.Add(comment);
        await _ctx.SaveChangesAsync(ct);

        // Notification — post sahibi != yorum yazan
        if (post.UserId != userId)
        {
            var commenterName = await GetDisplayNameAsync(userId, ct);
            var link = !string.IsNullOrEmpty(post.AuthorSlug)
                ? $"/uye/{post.AuthorSlug}/makale/{post.Slug}#yorum-{comment.Id}"
                : "/bildirimler";
            await SafeNotifyAsync(() => _notifications.CreateAsync(
                type: NotificationType.PostComment,
                userId: post.UserId,
                title: "Yorum yapıldı",
                body: $"{commenterName} makalenize yorum yaptı.",
                link: link,
                relatedEntityType: nameof(PostComment),
                relatedEntityId: comment.Id,
                ct: ct));
        }

        await _activityLog.LogAsync(
            action: "PostComment.Create",
            entityType: nameof(PostComment),
            entityId: comment.Id,
            description: $"Yorum eklendi: post={postId} user={userId}",
            metadata: new { PostId = postId, UserId = userId, comment.Id },
            ct: ct);

        return new PostCommentResult(true, null, comment);
    }

    public async Task<PostCommentResult> UpdateAsync(
        int commentId, int actingUserId, string rawBody, CancellationToken ct = default)
    {
        var comment = await _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
            return new PostCommentResult(false, "Yorum bulunamadı.", null);
        if (comment.UserId != actingUserId)
            return new PostCommentResult(false, "Bu yorumu düzenleyemezsiniz.", null);

        var validation = ValidateBody(rawBody);
        if (validation is not null) return validation;

        var processedBody = CommentBodyProcessor.Process(rawBody, _sanitizer);
        if (string.IsNullOrEmpty(processedBody))
            return new PostCommentResult(false, "Yorum içeriği boş olamaz.", null);
        if (processedBody.Length > 2000)
            processedBody = processedBody.Substring(0, 2000);

        // İçerik gerçekten değişmediyse no-op: IsEdited rozeti ve revision
        // oluşturma tetiklenmez (Faz 6.8 #21).
        if (string.Equals(comment.Body, processedBody, StringComparison.Ordinal))
            return new PostCommentResult(true, null, comment);

        // İlk düzenleme ise orijinali sakla. Sonraki düzenlemeler revision'a
        // DOKUNMAZ → her zaman ilk gönderilen hâli yansıtır.
        var hasRevision = await _ctx.PostCommentRevisions
            .AnyAsync(r => r.CommentId == comment.Id, ct);
        if (!hasRevision)
        {
            _ctx.PostCommentRevisions.Add(new PostCommentRevision
            {
                CommentId = comment.Id,
                OriginalBody = comment.Body,
                OriginalCreatedAt = comment.CreatedAt,
                FirstEditedAt = DateTime.UtcNow
            });
        }

        comment.Body = processedBody;
        comment.UpdatedAt = DateTime.UtcNow;
        comment.IsEdited = true;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostComment.Update",
            entityType: nameof(PostComment),
            entityId: comment.Id,
            description: $"Yorum düzenlendi: id={commentId}",
            metadata: new { CommentId = commentId, ActingUserId = actingUserId },
            ct: ct);

        return new PostCommentResult(true, null, comment);
    }

    public async Task<PostCommentResult> DeleteAsync(
        int commentId, int actingUserId, bool isAdmin, CancellationToken ct = default)
    {
        var comment = await _ctx.PostComments
            .Include(c => c.Post)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null)
            return new PostCommentResult(false, "Yorum bulunamadı.", null);

        var isOwner = comment.UserId == actingUserId;
        var isPostOwner = comment.Post != null && comment.Post.UserId == actingUserId;
        if (!isOwner && !isPostOwner && !isAdmin)
            return new PostCommentResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);

        var snapshotId = comment.Id;
        var snapshotPostId = comment.PostId;
        var deletedBy = isOwner ? "owner" : (isPostOwner ? "post-owner" : "admin");

        _ctx.PostComments.Remove(comment);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostComment.Delete",
            entityType: nameof(PostComment),
            entityId: snapshotId,
            description: $"Yorum silindi: id={snapshotId} by={deletedBy}",
            metadata: new { CommentId = snapshotId, PostId = snapshotPostId, ActingUserId = actingUserId, DeletedBy = deletedBy },
            ct: ct);

        return new PostCommentResult(true, null, null);
    }

    public async Task<IReadOnlyList<PostComment>> GetByPostIdAsync(
        int postId, bool includeHidden = false, CancellationToken ct = default)
    {
        var q = _ctx.PostComments
            .Include(c => c.User).ThenInclude(u => u!.Profile)
            .Where(c => c.PostId == postId);
        if (!includeHidden)
            q = q.Where(c => !c.IsModeratorHidden);
        return await q.OrderBy(c => c.CreatedAt).ToListAsync(ct);
    }

    public Task<int> GetCountForPostAsync(int postId, CancellationToken ct = default)
        => _ctx.PostComments.CountAsync(c => c.PostId == postId && !c.IsModeratorHidden, ct);

    public Task<PostComment?> GetByIdAsync(int commentId, CancellationToken ct = default)
        => _ctx.PostComments.FirstOrDefaultAsync(c => c.Id == commentId, ct);

    public Task<PostCommentRevision?> GetRevisionAsync(int commentId, CancellationToken ct = default)
        => _ctx.PostCommentRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.CommentId == commentId, ct);

    // ─── helpers ──────────────────────────────────────────────────────────

    private static PostCommentResult? ValidateBody(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return new PostCommentResult(false, "Yorum boş olamaz.", null);
        if (rawBody.Trim().Length > MaxRawBodyLength)
            return new PostCommentResult(false,
                $"Yorum {MaxRawBodyLength} karakteri aşamaz.", null);
        return null;
    }

    private async Task<string> GetDisplayNameAsync(int userId, CancellationToken ct)
    {
        var name = await _ctx.UserProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(name)) return name!;

        var fallback = await _ctx.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName ?? u.UserName)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(fallback) ? "Bir kullanıcı" : fallback!;
    }

    private async Task SafeNotifyAsync(Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "PostCommentService notification failed (asıl işlem etkilenmedi).");
        }
    }
}
