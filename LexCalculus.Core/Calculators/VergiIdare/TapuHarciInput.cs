using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>Harç yükümlüsü tarafı (UI gösterimi için).</summary>
public enum TapuHarcKisi
{
    [Display(Name = "Her İkisi (Alıcı + Satıcı)")]
    HerIkisi = 1,

    [Display(Name = "Yalnız Alıcı")]
    Alici = 2,

    [Display(Name = "Yalnız Satıcı")]
    Satici = 3
}

/// <summary>
/// G2 Tapu Harcı — 492 s.K. Tapu ve Kadastro Harçları Tarifesi. 2026 oranı
/// alıcı ve satıcı için ayrı ayrı %2 (toplam %4); parametre slug
/// <c>tapu-harci/oran</c> üzerinden okunur.
/// </summary>
public sealed class TapuHarciInput
{
    [Display(Name = "Satış Değeri (TL) — beyan")]
    [Required(ErrorMessage = "Satış değeri boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Satış değeri pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? SatisDegeri { get; set; }

    [Display(Name = "Harç Yükümlüsü")]
    public TapuHarcKisi KisiBasi { get; set; } = TapuHarcKisi.HerIkisi;

    [Display(Name = "Hesap Tarihi (oran referansı)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
