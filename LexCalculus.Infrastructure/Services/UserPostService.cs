using Ganss.Xss;
using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class UserPostService : IUserPostService
{
    /// <summary>Bir makaleye en fazla kaç tag eklenebilir (charter §2.2 Karar 12).</summary>
    private const int MaxTagsPerPost = 5;

    private readonly ApplicationDbContext _ctx;
    private readonly IPostTagService _tagService;
    private readonly IActivityLogService _activityLog;
    private readonly IHtmlSanitizer _sanitizer;

    public UserPostService(
        ApplicationDbContext ctx,
        IPostTagService tagService,
        IActivityLogService activityLog,
        IHtmlSanitizer sanitizer)
    {
        _ctx = ctx;
        _tagService = tagService;
        _activityLog = activityLog;
        _sanitizer = sanitizer;
    }

    public async Task<UserPostResult> CreateDraftAsync(
        int userId, UserPostInput input, CancellationToken ct = default)
    {
        var validation = await ValidateInputAsync(input, ct);
        if (validation is not null) return validation;

        var slug = await GenerateUniqueSlugAsync(userId, input.Title, excludePostId: null, ct);

        var now = DateTime.UtcNow;
        var post = new UserPost
        {
            UserId = userId,
            CategoryId = input.CategoryId,
            Slug = slug,
            Title = input.Title.Trim(),
            Body = _sanitizer.Sanitize(input.Body ?? string.Empty),
            FeaturedImageUrl = string.IsNullOrWhiteSpace(input.FeaturedImageUrl)
                ? null : input.FeaturedImageUrl.Trim(),
            IsPublished = false,
            PublishedAt = null,
            ViewCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        _ctx.UserPosts.Add(post);
        await _ctx.SaveChangesAsync(ct);

        // Tag link oluştur — draft olduğu için UsageCount artırma YOK
        await SyncTagsAsync(post, input.TagNames, isPublished: false, ct);

        await _activityLog.LogAsync(
            action: "UserPost.CreateDraft",
            entityType: nameof(UserPost),
            entityId: post.Id,
            description: $"Taslak oluşturuldu: {post.Title} ({post.Slug})",
            metadata: new { post.Id, post.Slug, post.CategoryId, UserId = userId },
            ct: ct);

        return new UserPostResult(true, null, post);
    }

    public async Task<UserPostResult> UpdateAsync(
        int postId, int actingUserId, UserPostInput input, CancellationToken ct = default)
    {
        var post = await _ctx.UserPosts
            .Include(p => p.TagLinks)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return new UserPostResult(false, "Makale bulunamadı.", null);
        if (post.UserId != actingUserId)
            return new UserPostResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);

        var validation = await ValidateInputAsync(input, ct);
        if (validation is not null) return validation;

        // Slug REGEN ETME (Yaklaşım 4) — Title değişse de slug sabit kalır
        post.Title = input.Title.Trim();
        post.Body = _sanitizer.Sanitize(input.Body ?? string.Empty);
        post.CategoryId = input.CategoryId;
        post.FeaturedImageUrl = string.IsNullOrWhiteSpace(input.FeaturedImageUrl)
            ? null : input.FeaturedImageUrl.Trim();
        post.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await SyncTagsAsync(post, input.TagNames, isPublished: post.IsPublished, ct);

        await _activityLog.LogAsync(
            action: "UserPost.Update",
            entityType: nameof(UserPost),
            entityId: post.Id,
            description: $"Makale güncellendi: {post.Title}",
            metadata: new { post.Id, post.Slug, ActingUserId = actingUserId },
            ct: ct);

        return new UserPostResult(true, null, post);
    }

    public async Task<UserPostResult> PublishAsync(
        int postId, int actingUserId, CancellationToken ct = default)
    {
        var post = await _ctx.UserPosts
            .Include(p => p.TagLinks)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return new UserPostResult(false, "Makale bulunamadı.", null);
        if (post.UserId != actingUserId)
            return new UserPostResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);
        if (post.IsPublished)
            return new UserPostResult(true, null, post); // idempotent

        post.IsPublished = true;
        post.PublishedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        // Tag UsageCount artır
        foreach (var link in post.TagLinks)
            await _tagService.IncrementUsageAsync(link.TagId, ct);

        await _activityLog.LogAsync(
            action: "UserPost.Publish",
            entityType: nameof(UserPost),
            entityId: post.Id,
            description: $"Makale yayınlandı: {post.Title}",
            metadata: new { post.Id, post.Slug, post.PublishedAt },
            ct: ct);

        return new UserPostResult(true, null, post);
    }

    public async Task<UserPostResult> UnpublishAsync(
        int postId, int actingUserId, CancellationToken ct = default)
    {
        var post = await _ctx.UserPosts
            .Include(p => p.TagLinks)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return new UserPostResult(false, "Makale bulunamadı.", null);
        if (post.UserId != actingUserId)
            return new UserPostResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);
        if (!post.IsPublished)
            return new UserPostResult(true, null, post); // idempotent

        post.IsPublished = false;
        // PublishedAt korunur — yayınlama tarihçesi (yeniden publish'te
        // override edilir).
        post.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        // Tag UsageCount azalt
        foreach (var link in post.TagLinks)
            await _tagService.DecrementUsageAsync(link.TagId, ct);

        await _activityLog.LogAsync(
            action: "UserPost.Unpublish",
            entityType: nameof(UserPost),
            entityId: post.Id,
            description: $"Makale taslağa alındı: {post.Title}",
            metadata: new { post.Id, post.Slug },
            ct: ct);

        return new UserPostResult(true, null, post);
    }

    public async Task<UserPostResult> DeleteAsync(
        int postId, int actingUserId, CancellationToken ct = default)
    {
        var post = await _ctx.UserPosts
            .Include(p => p.TagLinks)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return new UserPostResult(false, "Makale bulunamadı.", null);
        if (post.UserId != actingUserId)
            return new UserPostResult(false, "Bu işlemi yapmaya yetkiniz yok.", null);

        var wasPublished = post.IsPublished;
        var tagIds = post.TagLinks.Select(l => l.TagId).ToList();
        var snapshotId = post.Id;
        var snapshotTitle = post.Title;
        var snapshotSlug = post.Slug;

        // Cascade: PostTagLink satırları otomatik silinir.
        _ctx.UserPosts.Remove(post);
        await _ctx.SaveChangesAsync(ct);

        // Yayındaysa tag UsageCount'ları azalt (cascade UsageCount güncellemez)
        if (wasPublished)
        {
            foreach (var tagId in tagIds)
                await _tagService.DecrementUsageAsync(tagId, ct);
        }

        await _activityLog.LogAsync(
            action: "UserPost.Delete",
            entityType: nameof(UserPost),
            entityId: snapshotId,
            description: $"Makale silindi: {snapshotTitle} ({snapshotSlug})",
            metadata: new { Id = snapshotId, Slug = snapshotSlug, WasPublished = wasPublished, ActingUserId = actingUserId },
            ct: ct);

        return new UserPostResult(true, null, null);
    }

    public Task<UserPost?> GetByIdAsync(int postId, CancellationToken ct = default)
        => _ctx.UserPosts
            .Include(p => p.User).ThenInclude(u => u!.Profile)
            .Include(p => p.Category)
            .Include(p => p.TagLinks).ThenInclude(l => l.Tag)
            .FirstOrDefaultAsync(p => p.Id == postId, ct);

    public Task<UserPost?> GetByUserAndSlugAsync(
        int userId, string slug, CancellationToken ct = default)
        => _ctx.UserPosts
            .Include(p => p.User).ThenInclude(u => u!.Profile)
            .Include(p => p.Category)
            .Include(p => p.TagLinks).ThenInclude(l => l.Tag)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Slug == slug, ct);

    public async Task<IReadOnlyList<UserPost>> GetByUserIdAsync(
        int userId, bool includeUnpublished, CancellationToken ct = default)
    {
        var query = _ctx.UserPosts
            .Include(p => p.Category)
            .Include(p => p.TagLinks).ThenInclude(l => l.Tag)
            .Where(p => p.UserId == userId);

        if (!includeUnpublished)
            query = query.Where(p => p.IsPublished);

        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task<UserPostResult?> ValidateInputAsync(
        UserPostInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            return new UserPostResult(false, "Başlık boş olamaz.", null);
        if (input.Title.Trim().Length > 200)
            return new UserPostResult(false, "Başlık 200 karakteri aşamaz.", null);
        if (string.IsNullOrWhiteSpace(input.Body))
            return new UserPostResult(false, "İçerik boş olamaz.", null);
        if (input.TagNames.Count > MaxTagsPerPost)
            return new UserPostResult(false,
                $"En fazla {MaxTagsPerPost} etiket kullanılabilir.", null);

        var category = await _ctx.PostCategories
            .Where(c => c.Id == input.CategoryId)
            .Select(c => new { c.Id, c.IsActive })
            .FirstOrDefaultAsync(ct);
        if (category is null)
            return new UserPostResult(false, "Kategori bulunamadı.", null);
        if (!category.IsActive)
            return new UserPostResult(false, "Bu kategori şu anda aktif değil.", null);

        return null;
    }

    private async Task<string> GenerateUniqueSlugAsync(
        int userId, string title, int? excludePostId, CancellationToken ct)
    {
        var baseSlug = SlugHelper.Generate(title);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "makale";
        if (baseSlug.Length > 200) baseSlug = baseSlug.Substring(0, 200);

        var slug = baseSlug;
        var suffix = 2;
        while (await _ctx.UserPosts.AnyAsync(
            p => p.UserId == userId && p.Slug == slug
                && (excludePostId == null || p.Id != excludePostId), ct))
        {
            slug = $"{baseSlug}-{suffix}";
            if (slug.Length > 200) slug = slug.Substring(0, 200);
            suffix++;
        }
        return slug;
    }

    /// <summary>
    /// Post'un tag'lerini input listeyle senkronize eder. Set difference
    /// hesabı: kaldırılacaklar (eski - yeni) ve eklenecekler (yeni - eski).
    /// isPublished=true ise UsageCount delta güncellenir.
    /// </summary>
    private async Task SyncTagsAsync(
        UserPost post, IReadOnlyList<string> tagNames, bool isPublished, CancellationToken ct)
    {
        // Yeni tag'leri normalize et (slug bazında dedup)
        var desiredTags = new Dictionary<string, PostTag>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in tagNames)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var tag = await _tagService.GetOrCreateAsync(raw, ct);
            desiredTags[tag.Slug] = tag;
        }

        // Mevcut link'ler — tracking ile yüklenmiş olmalı (Include'lı çağrı sonrası)
        var existingLinks = post.TagLinks.ToList();
        var existingTagIds = existingLinks.Select(l => l.TagId).ToHashSet();
        var desiredTagIds = desiredTags.Values.Select(t => t.Id).ToHashSet();

        // Çıkarılacaklar — sadece DbSet.Remove (EF tracker nav koleksiyonu da fixup eder)
        var toRemove = existingLinks.Where(l => !desiredTagIds.Contains(l.TagId)).ToList();
        foreach (var link in toRemove)
        {
            _ctx.PostTagLinks.Remove(link);
            if (isPublished)
                await _tagService.DecrementUsageAsync(link.TagId, ct);
        }

        // Eklenecekler — sadece DbSet.Add (nav koleksiyona Add yapma; double-tracking olur)
        var toAddTagIds = desiredTagIds.Except(existingTagIds).ToList();
        foreach (var tagId in toAddTagIds)
        {
            var newLink = new PostTagLink
            {
                PostId = post.Id,
                TagId = tagId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.PostTagLinks.Add(newLink);
            if (isPublished)
                await _tagService.IncrementUsageAsync(tagId, ct);
        }

        if (toRemove.Count > 0 || toAddTagIds.Count > 0)
            await _ctx.SaveChangesAsync(ct);
    }
}
