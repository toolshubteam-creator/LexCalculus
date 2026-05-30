using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class PostTagService : IPostTagService
{
    private readonly ApplicationDbContext _ctx;

    public PostTagService(ApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<PostTag> GetOrCreateAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag adı boş olamaz.", nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length > 30)
            trimmed = trimmed.Substring(0, 30);

        var slug = SlugHelper.Generate(trimmed);
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("Tag adından geçerli bir slug üretilemedi.", nameof(name));

        var existing = await _ctx.PostTags.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (existing is not null) return existing;

        var tag = new PostTag
        {
            Name = trimmed,
            Slug = slug,
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.PostTags.Add(tag);

        try
        {
            await _ctx.SaveChangesAsync(ct);
            return tag;
        }
        catch (DbUpdateException)
        {
            // Race: paralel insert; mevcut tag'i fetch et.
            _ctx.Entry(tag).State = EntityState.Detached;
            return await _ctx.PostTags.FirstAsync(t => t.Slug == slug, ct);
        }
    }

    public Task<PostTag?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _ctx.PostTags.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<IReadOnlyList<PostTag>> GetPopularAsync(int limit = 20, CancellationToken ct = default)
        => await _ctx.PostTags
            .Where(t => t.UsageCount > 0)
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PostTag>> SearchByPrefixAsync(
        string prefix, int take, CancellationToken ct = default)
    {
        var normalized = (prefix ?? "").Trim().ToLowerInvariant();
        if (normalized.Length < 2) return Array.Empty<PostTag>();

        var clamped = Math.Clamp(take, 1, 20);
        return await _ctx.PostTags
            .Where(t => t.Name.ToLower().StartsWith(normalized))
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Take(clamped)
            .ToListAsync(ct);
    }

    public async Task IncrementUsageAsync(int tagId, CancellationToken ct = default)
    {
        var tag = await _ctx.PostTags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
        if (tag is null) return;
        tag.UsageCount += 1;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task DecrementUsageAsync(int tagId, CancellationToken ct = default)
    {
        var tag = await _ctx.PostTags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
        if (tag is null || tag.UsageCount <= 0) return;
        tag.UsageCount -= 1;
        await _ctx.SaveChangesAsync(ct);
    }
}
