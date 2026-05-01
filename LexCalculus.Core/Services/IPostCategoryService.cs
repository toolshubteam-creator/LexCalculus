using LexCalculus.Core.Entities.Content;

namespace LexCalculus.Core.Services;

/// <summary>
/// Makale kategorisi yönetim servisi (admin CRUD). Hard delete YOK —
/// kategori devre dışı bırakılır (mevcut makale referansları korunur).
/// Faz 4.5, charter §3.3.
/// </summary>
public interface IPostCategoryService
{
    /// <summary>Aktif kategoriler — makale yazma formu için.</summary>
    Task<IReadOnlyList<PostCategory>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Tüm kategoriler — admin liste.</summary>
    Task<IReadOnlyList<PostCategory>> GetAllAsync(CancellationToken ct = default);

    Task<PostCategory?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PostCategory?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<PostCategoryResult> CreateAsync(PostCategoryInput input, CancellationToken ct = default);
    Task<PostCategoryResult> UpdateAsync(int id, PostCategoryInput input, CancellationToken ct = default);
    Task<PostCategoryResult> DeactivateAsync(int id, CancellationToken ct = default);
    Task<PostCategoryResult> ReactivateAsync(int id, CancellationToken ct = default);
}

public sealed record PostCategoryInput(
    string Name, string? Description, int DisplayOrder, bool IsActive);

public sealed record PostCategoryResult(
    bool Success, string? ErrorMessage, PostCategory? Category);
