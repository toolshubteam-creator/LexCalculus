using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class PostLikeService : IPostLikeService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IActivityLogService _activityLog;

    public PostLikeService(ApplicationDbContext ctx, IActivityLogService activityLog)
    {
        _ctx = ctx;
        _activityLog = activityLog;
    }

    public async Task<PostLikeToggleResult> ToggleAsync(
        int postId, int userId, CancellationToken ct = default)
    {
        var post = await _ctx.UserPosts
            .Where(p => p.Id == postId)
            .Select(p => new { p.Id, p.IsPublished })
            .FirstOrDefaultAsync(ct);

        if (post is null)
            return new PostLikeToggleResult(false, false, 0, "Makale bulunamadı.");
        if (!post.IsPublished)
            return new PostLikeToggleResult(false, false, 0,
                "Yayında olmayan makale beğenilemez.");

        var existing = await _ctx.PostLikes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId, ct);

        bool isLiked;
        if (existing is not null)
        {
            _ctx.PostLikes.Remove(existing);
            await _ctx.SaveChangesAsync(ct);
            isLiked = false;

            await _activityLog.LogAsync(
                action: "PostLike.Remove",
                entityType: nameof(PostLike),
                entityId: existing.Id,
                description: $"Beğeni kaldırıldı: post={postId} user={userId}",
                metadata: new { PostId = postId, UserId = userId },
                ct: ct);
        }
        else
        {
            var like = new PostLike
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.PostLikes.Add(like);
            try
            {
                await _ctx.SaveChangesAsync(ct);
                isLiked = true;

                await _activityLog.LogAsync(
                    action: "PostLike.Create",
                    entityType: nameof(PostLike),
                    entityId: like.Id,
                    description: $"Beğeni eklendi: post={postId} user={userId}",
                    metadata: new { PostId = postId, UserId = userId },
                    ct: ct);
            }
            catch (DbUpdateException)
            {
                // Race: paralel toggle başka bir add yapmış; mevcut zaten var → liked.
                _ctx.Entry(like).State = EntityState.Detached;
                isLiked = true;
            }
        }

        var count = await GetCountForPostAsync(postId, ct);
        return new PostLikeToggleResult(true, isLiked, count, null);
    }

    public Task<bool> IsLikedByAsync(int postId, int userId, CancellationToken ct = default)
        => _ctx.PostLikes.AnyAsync(l => l.PostId == postId && l.UserId == userId, ct);

    public Task<int> GetCountForPostAsync(int postId, CancellationToken ct = default)
        => _ctx.PostLikes.CountAsync(l => l.PostId == postId, ct);
}
