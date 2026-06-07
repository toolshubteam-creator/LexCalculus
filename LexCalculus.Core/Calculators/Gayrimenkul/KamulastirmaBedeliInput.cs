using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>Type of the expropriated immovable — affects which valuation method fits best.</summary>
public enum TasinmazTuru
{
    [Display(Name = "Arsa")]
    Arsa = 1,

    [Display(Name = "Mesken")]
    Mesken = 2,

    [Display(Name = "Ticari")]
    Ticari = 3,

    [Display(Name = "Tarım Arazisi")]
    Tarim = 4,

    [Display(Name = "Bina")]
    Bina = 5
}

/// <summary>Valuation method selected by the user (2942 s.K. m.11).</summary>
public enum KamulastirmaYontemi
{
    [Display(Name = "Emsal Karşılaştırma")]
    EmsalKarsilastirma = 1,

    [Display(Name = "Gelir Kapitalizasyonu")]
    GelirKapitalizasyonu = 2
}

/// <summary>
/// Inputs for the expropriation value calculator (Kamulaştırma Bedeli, 2942 s.K. m.11).
/// Method-specific fields are validated inside the calculator based on Yontem.
/// </summary>
public sealed class KamulastirmaBedeliInput
{
    [Display(Name = "Taşınmaz Türü")]
    public TasinmazTuru TasinmazTuru { get; set; } = TasinmazTuru.Arsa;

    [Display(Name = "Yüzölçümü (m²)")]
    [Required(ErrorMessage = "Yüzölçümü boş olamaz.")]
    [Range(typeof(decimal), "0.01", "99999999",
        ErrorMessage = "Yüzölçümü pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Yuzolcumu { get; set; }

    [Display(Name = "Değerleme Yöntemi")]
    public KamulastirmaYontemi Yontem { get; set; } = KamulastirmaYontemi.EmsalKarsilastirma;

    // --- Emsal karşılaştırma yöntemi ---
    [Display(Name = "Emsal Birim Fiyat (TL/m²)")]
    [Range(typeof(decimal), "0", "99999999",
        ErrorMessage = "Emsal birim fiyat negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? EmsalBirimFiyat { get; set; }

    [Display(Name = "Objektif Değer Artış Oranı (%)")]
    [Range(0, 1000, ErrorMessage = "Objektif artış oranı negatif olamaz.")]
    public decimal ObjektifArtisOrani { get; set; }

    // --- Gelir kapitalizasyonu yöntemi ---
    [Display(Name = "Yıllık Net Gelir (TL)")]
    [Range(typeof(decimal), "0", "99999999",
        ErrorMessage = "Yıllık net gelir negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YillikNetGelir { get; set; }

    [Display(Name = "Kapitalizasyon Oranı (%)")]
    [Range(typeof(decimal), "0.01", "100",
        ErrorMessage = "Kapitalizasyon oranı 0.01-100 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? KapitalizasyonOrani { get; set; }

    // --- Yapı (bina) değeri — opsiyonel, her iki yöntemde eklenebilir ---
    [Display(Name = "Üzerinde Yapı Var")]
    public bool YapiVar { get; set; }

    [Display(Name = "Yapı Alanı (m²)")]
    [Range(typeof(decimal), "0", "99999999",
        ErrorMessage = "Yapı alanı negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YapiAlani { get; set; }

    [Display(Name = "Yapı Birim Maliyeti (TL/m²)")]
    [Range(typeof(decimal), "0", "99999999",
        ErrorMessage = "Yapı birim maliyeti negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YapiBirimMaliyet { get; set; }

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
