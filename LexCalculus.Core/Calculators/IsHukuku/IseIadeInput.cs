using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class IseIadeInput
{
    [Display(Name = "İşe Giriş Tarihi")]
    [Required(ErrorMessage = "Giriş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? GirisTarihi { get; set; }

    [Display(Name = "Fesih Tarihi")]
    [Required(ErrorMessage = "Fesih tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? FesihTarihi { get; set; }

    [Display(Name = "Karar / Hesap Tarihi")]
    [Required(ErrorMessage = "Karar tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? KararTarihi { get; set; }

    [Display(Name = "Brüt Aylık Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutAylikUcret { get; set; }

    [Display(Name = "İade Tazminatı Ay Sayısı (4-8)")]
    [Range(4, 8, ErrorMessage = "İade tazminatı 4-8 ay arasında olmalıdır.")]
    public int IadeAyiSayisi { get; set; } = 4;

    [Display(Name = "İşyerinde Çalışan Sayısı")]
    [Range(1, 99999, ErrorMessage = "Çalışan sayısı pozitif olmalıdır.")]
    public int IsyeriCalisanSayisi { get; set; } = 30;
}
