using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class GeciciIsGoremezlikInput
{
    [Display(Name = "Olay Tarihi")]
    [Required(ErrorMessage = "Olay tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OlayTarihi { get; set; }

    [Display(Name = "İş Göremezlik Süresi (gün)")]
    [Required(ErrorMessage = "Süre boş olamaz.")]
    [Range(1, 1825, ErrorMessage = "Süre 1-1825 (5 yıl) arası olmalıdır.")]
    public int? SureGun { get; set; }

    [Display(Name = "Aylık Brüt Ücret (TL)")]
    [Required(ErrorMessage = "Brüt ücret boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Brüt ücret pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AylikBrutUcret { get; set; }

    [Display(Name = "SGK Geçici İş Göremezlik Oranı (%)")]
    [Range(0, 100, ErrorMessage = "SGK oranı 0-100% arası olmalı.")]
    public decimal SgkOrani { get; set; } = 66.67m;

    [Display(Name = "SGK Ödeneği Mahsup Edilsin")]
    public bool SgkMahsup { get; set; } = true;
}
