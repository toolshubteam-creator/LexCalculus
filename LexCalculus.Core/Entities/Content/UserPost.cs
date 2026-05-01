using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Kullanıcı tarafından yazılan makale. Draft → Published state machine
/// (charter §2.2 Karar 9). Slug user namespace altında unique (charter Karar 11,
/// Yaklaşım 4: ilk üretimde sabit, kullanıcı değiştiremez).
/// Faz 4.6 P1.
/// </summary>
public sealed class UserPost
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int CategoryId { get; set; }
    public PostCategory Category { get; set; } = null!;

    public string Slug { get; set; } = null!;
    public string Title { get; set; } = null!;

    /// <summary>HTML içerik (Quill editor output, P3'te sanitize edilecek).</summary>
    public string Body { get; set; } = null!;

    /// <summary>MediaFile relative path (Adım 4.8'de gelecek görsel altyapısı).</summary>
    public string? FeaturedImageUrl { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Görüntülenme sayacı (long: 4B üzeri olası).</summary>
    public long ViewCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PostTagLink> TagLinks { get; set; } = new List<PostTagLink>();
}
