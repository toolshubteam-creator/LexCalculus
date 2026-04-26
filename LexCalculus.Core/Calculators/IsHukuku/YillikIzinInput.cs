using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Inputs for unused annual leave pay calculator.
/// </summary>
public sealed class YillikIzinInput
{
    [Display(Name = "İşe Giriş Tarihi")]
    [Required(ErrorMessage = "Giriş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? GirisTarihi { get; set; }

    [Display(Name = "İşten Ayrılış Tarihi")]
    [Required(ErrorMessage = "Çıkış tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? CikisTarihi { get; set; }

    [Display(Name = "Doğum Tarihi (yaş kontrolü için)")]
    [DataType(DataType.Date)]
    public DateTime? DogumTarihi { get; set; }

    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif bir sayı olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "Kullanılan Toplam İzin Günü")]
    [Range(0, 999, ErrorMessage = "Kullanılan izin negatif olamaz.")]
    public int KullanilanIzinGunu { get; set; } = 0;
}
