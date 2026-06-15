using LexCalculus.Core.Entities.Calculators;

namespace LexCalculus.Core.Services;

/// <summary>
/// Tek dilim hesabının detay satırı: bu dilimde vergilendirilen tutar ile
/// uygulanan oran ve sonuç vergi.
/// </summary>
public sealed class DilimDetay
{
    public required int Sira { get; init; }
    public required decimal MinAmount { get; init; }
    public required decimal? MaxAmount { get; init; }
    public required decimal DilimdekiTutar { get; init; }
    public required decimal Rate { get; init; }
    public required decimal DilimVergisi { get; init; }
}

/// <summary>
/// Dilim bazlı (progresif) vergi hesabının sonucu. Toplam vergi + dilim
/// dökümü; UI'da dilim tablosu olarak gösterilir.
/// </summary>
public sealed class DilimliVergiSonuc
{
    public required decimal ToplamVergi { get; init; }
    public required IReadOnlyList<DilimDetay> DilimDetaylar { get; init; }
}

/// <summary>
/// Tax bracket (vergi dilim) sorgu + dilim bazlı vergi hesabı servisi
/// (Charter Karar 1). G1 Veraset, G3 Damga (Faz 7.10), Faz 8+ gelir vergisi
/// tarafından paylaşılır.
///
/// LOOKUP SEMANTIC: <see cref="GetBracketsAsync"/> verilen <c>asOf</c>'a göre
/// EN GÜNCEL (latest EffectiveDate &lt;= asOf) dilim setini Sira sırasında
/// döner. Cache TTL 24 saat (FormulaParameter ile aynı).
///
/// <see cref="HesaplaAsync"/> marjinal dilim formülünü uygular: her dilim
/// sınırları arasında kalan tutar × dilim oranı kümülatif olarak toplanır.
/// Bir önceki dilim doluyken sonraki diliminde hâlâ tutar varsa devam eder;
/// son dilimde MaxAmount=null sınırsız davranır.
/// </summary>
public interface ITaxBracketService
{
    /// <summary>Belirli tarihte aktif dilim setini Sira sırasında döner.</summary>
    Task<IReadOnlyList<TaxBracket>> GetBracketsAsync(string toolSlug, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Vergilendirilebilir tutar üzerinde marjinal dilim hesabı uygular.
    /// 0 veya negatif tutar için toplam vergi 0 döner.
    /// </summary>
    Task<DilimliVergiSonuc> HesaplaAsync(string toolSlug, decimal vergilendirilebilirTutar, DateTime asOf, CancellationToken ct = default);
}
