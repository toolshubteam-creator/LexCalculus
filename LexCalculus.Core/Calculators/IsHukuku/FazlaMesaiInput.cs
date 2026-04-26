using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Inputs for overtime pay calculator. Phase 2 simplification: user enters
/// aggregate overtime hours per category (regular OT, weekly rest day work,
/// holiday work). Phase 5 may add a per-week timesheet input mode.
/// </summary>
public sealed class FazlaMesaiInput
{
    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif bir sayı olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "Haftalık Normal Çalışma Süresi (saat)")]
    [Range(1, 60, ErrorMessage = "Haftalık süre 1-60 saat arası olmalı.")]
    public int HaftalikNormalSaat { get; set; } = 45;

    [Display(Name = "Toplam Fazla Mesai Saati (45 saat üzeri)")]
    [Range(0, 99999, ErrorMessage = "Negatif olamaz.")]
    public int FazlaMesaiSaati { get; set; } = 0;

    [Display(Name = "Hafta Tatili Çalışma Saati")]
    [Range(0, 99999, ErrorMessage = "Negatif olamaz.")]
    public int HaftaTatiliSaati { get; set; } = 0;

    [Display(Name = "Bayram / Genel Tatil Çalışma Saati")]
    [Range(0, 99999, ErrorMessage = "Negatif olamaz.")]
    public int BayramSaati { get; set; } = 0;

    [Display(Name = "Hesap Tarihi (vergi oranları için)")]
    [Required]
    [DataType(DataType.Date)]
    public DateTime? HesapTarihi { get; set; } = DateTime.Today;
}
