using LexCalculus.Core.Entities.Content;

namespace LexCalculus.Core.Services;

/// <summary>
/// Kullanıcı makalesi yönetim servisi. Draft/Published state machine
/// (charter §2.2 Karar 9). Slug ilk üretimde sabit (Karar 11, Yaklaşım 4).
/// Tag UsageCount stratejisi: yalnızca yayında olan post'ların tag'leri sayılır
/// (publish'te artar, unpublish/delete'te azalır).
///
/// UI Adım 4.6 P2 (/makalelerim) ve P3 (Quill editor) ile gelecek.
/// </summary>
public interface IUserPostService
{
    Task<UserPostResult> CreateDraftAsync(int userId, UserPostInput input, CancellationToken ct = default);
    Task<UserPostResult> UpdateAsync(int postId, int actingUserId, UserPostInput input, CancellationToken ct = default);
    Task<UserPostResult> PublishAsync(int postId, int actingUserId, CancellationToken ct = default);
    Task<UserPostResult> UnpublishAsync(int postId, int actingUserId, CancellationToken ct = default);
    Task<UserPostResult> DeleteAsync(int postId, int actingUserId, CancellationToken ct = default);

    Task<UserPost?> GetByIdAsync(int postId, CancellationToken ct = default);
    Task<UserPost?> GetByUserAndSlugAsync(int userId, string slug, CancellationToken ct = default);

    /// <summary>
    /// includeUnpublished=true → sahip görünümü (taslak dahil); false →
    /// public görünüm (sadece published).
    /// </summary>
    Task<IReadOnlyList<UserPost>> GetByUserIdAsync(int userId, bool includeUnpublished, CancellationToken ct = default);
}

public sealed record UserPostInput(
    string Title,
    string Body,
    int CategoryId,
    string? FeaturedImageUrl,
    IReadOnlyList<string> TagNames);

public sealed record UserPostResult(bool Success, string? ErrorMessage, UserPost? Post);
