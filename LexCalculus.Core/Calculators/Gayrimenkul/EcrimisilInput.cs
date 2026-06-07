using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>Whether the entered rayiç kira figure is monthly or yearly.</summary>
public enum EcrimisilDonemTuru
{
    [Display(Name = "Aylık")]
    Aylik = 1,

    [Display(Name = "Yıllık")]
    Yillik = 2
}

/// <summary>
/// Inputs for the ecrimisil (unjust occupation compensation) calculator,
/// TMK m.995 + Yargıtay 1. HD yerleşik içtihadı. The first-period rayiç kira is
/// determined by an expert; later periods are escalated by the cumulative ÜFE
/// increase (Yargıtay uses ÜFE, not TÜFE).
/// </summary>
public sealed class EcrimisilInput
{
    [Display(Name = "İşgal Başlangıç Tarihi")]
    [Required(ErrorMessage = "İşgal başlangıç tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? IsgalBaslangic { get; set; }

    [Display(Name = "İşgal Bitiş Tarihi")]
    [Required(ErrorMessage = "İşgal bitiş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? IsgalBitis { get; set; }

    [Display(Name = "İlk Dönem Rayiç Kira (TL)")]
    [Required(ErrorMessage = "İlk dönem rayiç kira boş olamaz.")]
    [Range(typeof(decimal), "0.01", "99999999",
        ErrorMessage = "Rayiç kira pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? IlkDonemRayicKira { get; set; }

    [Display(Name = "Rayiç Kira Dönemi")]
    public EcrimisilDonemTuru DonemTuru { get; set; } = EcrimisilDonemTuru.Aylik;

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
