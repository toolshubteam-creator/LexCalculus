namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Serbest tag (kullanıcı tarafından makale yazımında üretilir, admin
/// onayı yok). Slug unique; aynı kavram için tek satır. UsageCount popüler
/// tag'leri yüzeye çıkarmak için tutulur.
/// Faz 4.5 — charter §3.3, Karar 12 (karma tag stratejisi).
/// </summary>
public sealed class PostTag
{
    public int Id { get; set; }

    /// <summary>Görüntüleme adı; case korunur (slug normalize'tan ayrı).</summary>
    public string Name { get; set; } = null!;

    /// <summary>URL-safe slug; lookup ve unique key burada.</summary>
    public string Slug { get; set; } = null!;

    /// <summary>Bu tag'i kullanan aktif makale sayısı.</summary>
    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
