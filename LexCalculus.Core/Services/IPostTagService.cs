using LexCalculus.Core.Entities.Content;

namespace LexCalculus.Core.Services;

/// <summary>
/// Serbest tag servisi. Makale yazımında kullanıcı tag yazar; servis
/// slug ile lookup yapar, yoksa yaratır (auto-create). Race koruması var.
/// Faz 4.5, charter §3.3, Karar 12.
/// </summary>
public interface IPostTagService
{
    /// <summary>
    /// Slug'dan lookup; yoksa yeni tag oluştur. UsageCount değiştirmez —
    /// post create akışında IncrementUsageAsync ayrı çağrılır.
    /// Race-safe: aynı slug için paralel insert'te ikinci çağrı mevcut tag'i fetch eder.
    /// </summary>
    Task<PostTag> GetOrCreateAsync(string name, CancellationToken ct = default);

    Task<PostTag?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>UsageCount DESC + Name ASC, ilk N tag (popüler tag widget).</summary>
    Task<IReadOnlyList<PostTag>> GetPopularAsync(int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Tag autocomplete (Faz 6.6, charter Karar 5). Prefix ile başlayan tag'ler,
    /// UsageCount DESC + Name ASC. Min 2 karakter (kısa/boş → boş liste);
    /// take 1-20 clamp (DoS önleme).
    /// </summary>
    Task<IReadOnlyList<PostTag>> SearchByPrefixAsync(string prefix, int take, CancellationToken ct = default);

    Task IncrementUsageAsync(int tagId, CancellationToken ct = default);

    /// <summary>Decrement; UsageCount 0'ın altına düşmez (defansif).</summary>
    Task DecrementUsageAsync(int tagId, CancellationToken ct = default);
}
