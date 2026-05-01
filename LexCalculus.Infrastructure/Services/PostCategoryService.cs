using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class PostCategoryService : IPostCategoryService
{
    private readonly ApplicationDbContext _ctx;
    private readonly IActivityLogService _activityLog;

    public PostCategoryService(ApplicationDbContext ctx, IActivityLogService activityLog)
    {
        _ctx = ctx;
        _activityLog = activityLog;
    }

    public async Task<IReadOnlyList<PostCategory>> GetActiveAsync(CancellationToken ct = default)
        => await _ctx.PostCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PostCategory>> GetAllAsync(CancellationToken ct = default)
        => await _ctx.PostCategories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    public Task<PostCategory?> GetByIdAsync(int id, CancellationToken ct = default)
        => _ctx.PostCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<PostCategory?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _ctx.PostCategories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<PostCategoryResult> CreateAsync(
        PostCategoryInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return new PostCategoryResult(false, "Kategori adı boş olamaz.", null);

        var slug = SlugHelper.Generate(input.Name);
        if (string.IsNullOrEmpty(slug))
            return new PostCategoryResult(false,
                "Kategori adından geçerli bir slug üretilemedi.", null);

        var clash = await _ctx.PostCategories.AnyAsync(c => c.Slug == slug, ct);
        if (clash)
            return new PostCategoryResult(false,
                "Bu kategori zaten mevcut (slug çakışması).", null);

        var category = new PostCategory
        {
            Name = input.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(input.Description)
                ? null : input.Description.Trim(),
            DisplayOrder = input.DisplayOrder,
            IsActive = input.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.PostCategories.Add(category);
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostCategory.Create",
            entityType: nameof(PostCategory),
            entityId: category.Id,
            description: $"Kategori oluşturuldu: {category.Name} ({category.Slug})",
            metadata: new { category.Id, category.Name, category.Slug },
            ct: ct);

        return new PostCategoryResult(true, null, category);
    }

    public async Task<PostCategoryResult> UpdateAsync(
        int id, PostCategoryInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return new PostCategoryResult(false, "Kategori adı boş olamaz.", null);

        var category = await _ctx.PostCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return new PostCategoryResult(false, "Kategori bulunamadı.", null);

        var trimmedName = input.Name.Trim();
        var nameChanged = !string.Equals(category.Name, trimmedName, StringComparison.Ordinal);

        if (nameChanged)
        {
            var newSlug = SlugHelper.Generate(trimmedName);
            if (string.IsNullOrEmpty(newSlug))
                return new PostCategoryResult(false,
                    "Kategori adından geçerli bir slug üretilemedi.", null);

            if (newSlug != category.Slug)
            {
                var clash = await _ctx.PostCategories
                    .AnyAsync(c => c.Slug == newSlug && c.Id != id, ct);
                if (clash)
                    return new PostCategoryResult(false,
                        "Bu kategori zaten mevcut (slug çakışması).", null);
                category.Slug = newSlug;
            }
            category.Name = trimmedName;
        }

        category.Description = string.IsNullOrWhiteSpace(input.Description)
            ? null : input.Description.Trim();
        category.DisplayOrder = input.DisplayOrder;
        category.IsActive = input.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostCategory.Update",
            entityType: nameof(PostCategory),
            entityId: category.Id,
            description: $"Kategori güncellendi: {category.Name} ({category.Slug})",
            metadata: new { category.Id, category.Name, category.Slug, category.IsActive },
            ct: ct);

        return new PostCategoryResult(true, null, category);
    }

    public async Task<PostCategoryResult> DeactivateAsync(
        int id, CancellationToken ct = default)
    {
        var category = await _ctx.PostCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return new PostCategoryResult(false, "Kategori bulunamadı.", null);

        if (!category.IsActive)
            return new PostCategoryResult(true, null, category); // idempotent

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostCategory.Deactivate",
            entityType: nameof(PostCategory),
            entityId: category.Id,
            description: $"Kategori devre dışı bırakıldı: {category.Name}",
            metadata: new { category.Id, category.Slug },
            ct: ct);

        return new PostCategoryResult(true, null, category);
    }

    public async Task<PostCategoryResult> ReactivateAsync(
        int id, CancellationToken ct = default)
    {
        var category = await _ctx.PostCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return new PostCategoryResult(false, "Kategori bulunamadı.", null);

        if (category.IsActive)
            return new PostCategoryResult(true, null, category);

        category.IsActive = true;
        category.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "PostCategory.Reactivate",
            entityType: nameof(PostCategory),
            entityId: category.Id,
            description: $"Kategori yeniden etkinleştirildi: {category.Name}",
            metadata: new { category.Id, category.Slug },
            ct: ct);

        return new PostCategoryResult(true, null, category);
    }
}
