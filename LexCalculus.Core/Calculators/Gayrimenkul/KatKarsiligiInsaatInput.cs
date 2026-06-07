using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>Distribution method for the kat karşılığı (construction-in-return) share.</summary>
public enum KatKarsiligiYontemi
{
    [Display(Name = "Oransal (Değer Bazlı)")]
    Oransal = 1,

    [Display(Name = "Sabit Oran (Sözleşme)")]
    Sabit = 2
}

/// <summary>
/// Inputs for the kat karşılığı inşaat share calculator (TBK + içtihat). The
/// owner and contractor split the total project value either proportionally
/// (land value / (land value + construction cost)) or by a fixed contractual
/// ratio. Domain rules are validated inside the calculator.
/// </summary>
public sealed class KatKarsiligiInsaatInput
{
    [Display(Name = "Paylaşım Yöntemi")]
    public KatKarsiligiYontemi Yontem { get; set; } = KatKarsiligiYontemi.Oransal;

    [Display(Name = "Arsa Değeri (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Arsa değeri negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ArsaDegeri { get; set; }

    [Display(Name = "Toplam İnşaat Maliyeti (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "İnşaat maliyeti negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamInsaatMaliyeti { get; set; }

    [Display(Name = "Toplam Proje Değeri (TL)")]
    [Required(ErrorMessage = "Toplam proje değeri boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Toplam proje değeri pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamProjeDegeri { get; set; }

    [Display(Name = "Arsa Sahibi Sabit Oranı (%)")]
    [Range(0, 100, ErrorMessage = "Arsa sahibi oranı %0-100 arası olmalıdır.")]
    public decimal ArsaSahibiOrani { get; set; }

    [Display(Name = "Toplam Bağımsız Bölüm Sayısı (opsiyonel)")]
    [Range(0, 100000, ErrorMessage = "Bağımsız bölüm sayısı negatif olamaz.")]
    public int? ToplamBagimsizBolumSayisi { get; set; }

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
