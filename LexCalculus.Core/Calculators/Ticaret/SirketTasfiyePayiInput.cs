using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>İmtiyazlı pay sahibi (esas sözleşmeyle tanımlı, TTK m.478/3).</summary>
public sealed class ImtiyazliPayGirdi
{
    [Display(Name = "Ortak Adı / Tanım")]
    public string? OrtakAdi { get; set; }

    [Display(Name = "Pay Adedi")]
    [Range(0, 1_000_000, ErrorMessage = "Pay adedi 0-1.000.000 arası olmalıdır.")]
    public int PayAdedi { get; set; }

    /// <summary>
    /// Esas sözleşmede tanımlı imtiyaz oranı (net varlığın yüzdesi olarak
    /// imtiyazlı blok payı). 0.10 = %10.
    /// </summary>
    [Display(Name = "İmtiyaz Oranı (%, net varlık üzerinden)")]
    [Range(typeof(decimal), "0", "100",
        ErrorMessage = "İmtiyaz oranı 0-100 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal ImtiyazOraniYuzde { get; set; }
}

/// <summary>
/// H1 Şirket Tasfiye Payı — TTK m.543 (Anonim Şirket tasfiyesi) + m.642
/// (Limited Şirket tasfiyesi). Net tasfiye varlığı belirlenir; varsa
/// imtiyazlı pay sahiplerine esas sözleşmede tanımlı oranlar uygulanır,
/// kalan standart ortaklara eşit dağıtılır.
/// </summary>
public sealed class SirketTasfiyePayiInput
{
    [Display(Name = "Toplam Varlık (TL)")]
    [Required(ErrorMessage = "Toplam varlık boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Toplam varlık negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamVarlik { get; set; }

    [Display(Name = "Toplam Borç (TL)")]
    [Required(ErrorMessage = "Toplam borç boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Toplam borç negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamBorc { get; set; }

    [Display(Name = "Standart Ortak Sayısı (imtiyazlı dışı)")]
    [Range(0, 10_000, ErrorMessage = "Standart ortak sayısı 0-10.000 arası olmalıdır.")]
    public int StandartOrtakSayisi { get; set; } = 1;

    public List<ImtiyazliPayGirdi> ImtiyazliPaylar { get; set; } = new();
}
