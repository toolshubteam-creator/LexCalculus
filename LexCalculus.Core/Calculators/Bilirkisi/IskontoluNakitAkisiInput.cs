using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I2 İskontolu Nakit Akışı — sonlu süreli yıllık gelirin bugünkü değeri
/// (Yargıtay HGK anüite içtihatları). IActuarialService.AnnuityPresentValue
/// reuse.
/// </summary>
public sealed class IskontoluNakitAkisiInput
{
    [Display(Name = "Yıllık Net Gelir (TL)")]
    [Required(ErrorMessage = "Yıllık net gelir boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Yıllık net gelir pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YillikNetGelir { get; set; }

    [Display(Name = "İskonto Oranı (%, yıllık)")]
    [Required(ErrorMessage = "İskonto oranı boş olamaz.")]
    [Range(typeof(decimal), "0", "100",
        ErrorMessage = "İskonto oranı 0-100 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? IskontoOraniYuzde { get; set; }

    [Display(Name = "Yıl Sayısı (N)")]
    [Required(ErrorMessage = "Yıl sayısı boş olamaz.")]
    [Range(1, 200, ErrorMessage = "Yıl sayısı 1-200 arası olmalıdır.")]
    public int? YilSayisi { get; set; }
}
