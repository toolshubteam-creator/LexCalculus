using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Inputs for minimum wage compliance check. The user enters the period and
/// the actual gross wage paid; the calculator compares against the statutory
/// minimum wage that was in force during each month of the period.
/// </summary>
public sealed class AsgariUcretInput
{
    [Display(Name = "Dönem Başlangıcı")]
    [Required(ErrorMessage = "Başlangıç tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? BaslangicTarihi { get; set; }

    [Display(Name = "Dönem Sonu")]
    [Required(ErrorMessage = "Bitiş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? BitisTarihi { get; set; }

    [Display(Name = "Ödenen Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Ödenen ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Ödenen ücret pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? OdenenBrutAylik { get; set; }

    [Display(Name = "Yasal Faiz Hesaplansın")]
    public bool FaizDahil { get; set; } = true;
}
