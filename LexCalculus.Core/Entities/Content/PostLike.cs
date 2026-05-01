using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Bir kullanıcının bir makaleyi beğenmesi. (PostId, UserId) composite UNIQUE
/// — bir kullanıcı bir post'u sadece bir kez beğenir. Toggle pattern: varsa
/// sil, yoksa ekle. Notification YOK (charter §2.4 Karar 22 — like sessiz).
/// Faz 4.9 P1.
/// </summary>
public sealed class PostLike
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public UserPost Post { get; set; } = null!;

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
