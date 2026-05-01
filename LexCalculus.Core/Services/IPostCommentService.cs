using LexCalculus.Core.Entities.Content;

namespace LexCalculus.Core.Services;

/// <summary>
/// Makale yorum servisi. Body işleme: CommentBodyProcessor (HtmlEncode +
/// auto-link + sanitize). Yetki: sahip düzenler; sahip + post sahibi + admin
/// siler. Faz 4.9 P1, charter §3.3.
/// </summary>
public interface IPostCommentService
{
    /// <summary>Yeni yorum oluştur. Post yayında olmalı, raw body 1-1000 char.</summary>
    Task<PostCommentResult> CreateAsync(
        int postId, int userId, string rawBody, CancellationToken ct = default);

    /// <summary>Mevcut yorumu düzenle. Sadece sahip; IsEdited=true set edilir.</summary>
    Task<PostCommentResult> UpdateAsync(
        int commentId, int actingUserId, string rawBody, CancellationToken ct = default);

    /// <summary>Yorumu sil. Yetki: sahip || post sahibi || admin.</summary>
    Task<PostCommentResult> DeleteAsync(
        int commentId, int actingUserId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Post yorumları, OrderBy(CreatedAt ASC) — eski → yeni.</summary>
    Task<IReadOnlyList<PostComment>> GetByPostIdAsync(int postId, CancellationToken ct = default);
    Task<int> GetCountForPostAsync(int postId, CancellationToken ct = default);
    Task<PostComment?> GetByIdAsync(int commentId, CancellationToken ct = default);
}

public sealed record PostCommentResult(bool Success, string? ErrorMessage, PostComment? Comment);
