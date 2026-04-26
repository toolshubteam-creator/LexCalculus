using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Inputs for the severance pay calculator. Validation attributes are surface
/// constraints (controller's ModelState handles them); domain rules (giriş
/// before çıkış, etc.) are enforced inside the calculator.
/// </summary>
public sealed class KidemTazminatiInput
{
    [Display(Name = "İşe Giriş Tarihi")]
    [Required(ErrorMessage = "Giriş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? GirisTarihi { get; set; }

    [Display(Name = "İşten Ayrılış Tarihi")]
    [Required(ErrorMessage = "Çıkış tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? CikisTarihi { get; set; }

    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Brüt ücret pozitif bir sayı olmalıdır.")]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "Yan Ödemeler (Aylık Karşılık, TL)")]
    [Range(typeof(decimal), "0", "999999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Yan ödemeler negatif olamaz.")]
    public decimal YanOdemelerAylik { get; set; } = 0m;

    [Display(Name = "İhbar Tazminatı Da Hesaplansın")]
    public bool IhbarDahil { get; set; }
}
