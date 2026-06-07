using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>Whether the entered ciro / kira figures are monthly or yearly.</summary>
public enum HasilatDonemTuru
{
    [Display(Name = "Aylık")]
    Aylik = 1,

    [Display(Name = "Yıllık")]
    Yillik = 2
}

/// <summary>
/// Inputs for the hâsılat (turnover-based) rent calculator — the AVM/shopping
/// mall rent model: rent = ciro × hâsılat oranı, optionally floored by a minimum
/// guarantee and capped by a maximum. Domain rules validated in the calculator.
/// </summary>
public sealed class HasilatKiraInput
{
    [Display(Name = "Ciro (TL)")]
    [Required(ErrorMessage = "Ciro boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Ciro negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Ciro { get; set; }

    [Display(Name = "Hâsılat Oranı (%)")]
    [Required(ErrorMessage = "Hâsılat oranı boş olamaz.")]
    [Range(typeof(decimal), "0.01", "100",
        ErrorMessage = "Hâsılat oranı %0.01-100 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? HasilatOrani { get; set; }

    [Display(Name = "Minimum Kira Güvencesi (TL, opsiyonel)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Minimum kira negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MinimumKira { get; set; }

    [Display(Name = "Maksimum Kira Tavanı (TL, opsiyonel)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Maksimum kira negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MaksimumKira { get; set; }

    [Display(Name = "Dönem")]
    public HasilatDonemTuru DonemTuru { get; set; } = HasilatDonemTuru.Aylik;

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
