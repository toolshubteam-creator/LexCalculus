using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Usage type of a bağımsız bölüm (independent unit). Drives the value
/// coefficient applied on top of the raw floor area when computing arsa payı.
/// </summary>
public enum KullanimTuru
{
    [Display(Name = "Mesken")]
    Mesken = 1,

    [Display(Name = "Dükkan")]
    Dukkan = 2,

    [Display(Name = "Bodrum")]
    Bodrum = 3,

    [Display(Name = "Çatı Katı")]
    CatiKati = 4
}

/// <summary>
/// One independent unit (bağımsız bölüm) in the building. Mutable + nullable to
/// support MVC model binding from indexed form fields (BagimsizBolumler[i].X)
/// and JSON restore from history, matching the SozlesmeOranDonem pattern.
/// </summary>
public sealed class BagimsizBolumGirdi
{
    [Display(Name = "Tanım")]
    [Required(ErrorMessage = "Bağımsız bölüm tanımı boş olamaz.")]
    public string? Tanim { get; set; }

    [Display(Name = "Yüzölçümü (m²)")]
    [Required(ErrorMessage = "Yüzölçümü boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999",
        ErrorMessage = "Yüzölçümü pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Yuzolcumu { get; set; }

    [Display(Name = "Kullanım Türü")]
    public KullanimTuru KullanimTuru { get; set; } = KullanimTuru.Mesken;

    [Display(Name = "Kat")]
    [Range(-5, 100, ErrorMessage = "Kat numarası -5 ile 100 arası olmalıdır.")]
    public int KatNumarasi { get; set; }
}

/// <summary>
/// Inputs for the arsa payı (land share) calculator. Domain rules (at least one
/// unit, positive areas) are enforced inside the calculator and surfaced via
/// CalculationResult.ValidationErrors, not by throwing.
/// </summary>
public sealed class ArsaPayiInput
{
    [Display(Name = "Bağımsız Bölümler")]
    public List<BagimsizBolumGirdi> BagimsizBolumler { get; set; } = new();

    /// <summary>
    /// Date used for time-versioned coefficient lookup. Defaults to "today" at
    /// bind time; coefficients rarely change but the lookup keeps the door open.
    /// </summary>
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
