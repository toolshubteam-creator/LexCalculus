using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I4 Çevresel Zarar Tazminatı — 2872 s.K. (Çevre Kanunu) m.28 kapsamında
/// doğrudan zarar + restorasyon maliyeti + ekosistem kaybı kalem toplamı;
/// opsiyonel bilirkişi maliyet eklenir.
/// </summary>
public sealed class CevreselZararInput
{
    [Display(Name = "Doğrudan Zarar (TL) — varlık değer kaybı")]
    [Required(ErrorMessage = "Doğrudan zarar boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Doğrudan zarar negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? DogrudanZarar { get; set; }

    [Display(Name = "Restorasyon Maliyeti (TL) — alan onarımı")]
    [Required(ErrorMessage = "Restorasyon maliyeti boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Restorasyon maliyeti negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? RestorasyonMaliyeti { get; set; }

    [Display(Name = "Ekosistem Kaybı (TL) — uzun vadeli hizmet kaybı tahmini")]
    [Required(ErrorMessage = "Ekosistem kaybı boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Ekosistem kaybı negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? EkosistemKaybi { get; set; }

    /// <summary>Bilirkişi/uzman maliyetinin toplam zarar üzerinden yüzde oranı (opsiyonel).</summary>
    [Display(Name = "Bilirkişi Maliyet Oranı (%, 0-15) — opsiyonel")]
    [Range(typeof(decimal), "0", "15",
        ErrorMessage = "Bilirkişi maliyet oranı 0-15 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BilirkisiMaliyetOraniYuzde { get; set; } = 0m;
}
