using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Faiz;

/// <summary>
/// One contract rate period. The rate applies from BaslangicTarihi until the
/// next period's BaslangicTarihi (or HesapTarihi if last).
/// </summary>
public sealed class SozlesmeOranDonem
{
    [Display(Name = "Bu Dönemin Başlangıcı")]
    [Required(ErrorMessage = "Dönem başlangıç tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? BaslangicTarihi { get; set; }

    [Display(Name = "Yıllık Faiz Oranı (%)")]
    [Required(ErrorMessage = "Faiz oranı boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999",
        ErrorMessage = "Faiz oranı 0.01 - 999 arası olmalı.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YillikOran { get; set; }
}
