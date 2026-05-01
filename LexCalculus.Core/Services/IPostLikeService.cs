namespace LexCalculus.Core.Services;

/// <summary>
/// Makale beğeni servisi. Toggle pattern: varsa kaldır, yoksa ekle. Bir
/// kullanıcı bir post'u tek kez beğenir (composite UNIQUE). Notification YOK
/// (charter Karar 22 — like sessiz, vatandaş gürültüsüzlüğü). Faz 4.9 P1.
/// </summary>
public interface IPostLikeService
{
    Task<PostLikeToggleResult> ToggleAsync(int postId, int userId, CancellationToken ct = default);
    Task<bool> IsLikedByAsync(int postId, int userId, CancellationToken ct = default);
    Task<int> GetCountForPostAsync(int postId, CancellationToken ct = default);
}

public sealed record PostLikeToggleResult(
    bool Success, bool IsLiked, int LikeCount, string? ErrorMessage);
