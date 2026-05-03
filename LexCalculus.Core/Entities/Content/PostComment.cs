using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Makaleye yapılan yorum. Body sanitize edilmiş HTML (sınırlı whitelist:
/// &lt;a&gt;, &lt;br&gt;). HtmlEncode + URL auto-link sunucuda yapılır.
/// Faz 4.9 P1.
/// </summary>
public sealed class PostComment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public UserPost Post { get; set; } = null!;

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Sunucuda işlenmiş HTML (HtmlEncode + auto-link + sanitize).</summary>
    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>True ise UI 'düzenlendi' rozeti gösterir.</summary>
    public bool IsEdited { get; set; }

    /// <summary>
    /// Admin moderasyon: gizle (Faz 5.3 Karar 11). True ise public listede
    /// görünmez; admin override ile görür. Yorum sayım'ından da düşer.
    /// </summary>
    public bool IsModeratorHidden { get; set; }
}
