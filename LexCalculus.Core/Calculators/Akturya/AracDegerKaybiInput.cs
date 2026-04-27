using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class AracDegerKaybiInput
{
    [Display(Name = "Olay Tarihi")]
    [Required(ErrorMessage = "Olay tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OlayTarihi { get; set; }

    [Display(Name = "Aracın Kazadan Önceki Değeri (TL)")]
    [Required(ErrorMessage = "Kazadan önceki değer boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Değer pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? KazadanOncekiDeger { get; set; }

    [Display(Name = "Aracın Kazadan Sonraki Değeri (TL)")]
    [Required(ErrorMessage = "Kazadan sonraki değer boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Değer pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? KazadanSonrakiDeger { get; set; }

    [Display(Name = "Aracın Yaşı (yıl)")]
    [Required(ErrorMessage = "Araç yaşı boş olamaz.")]
    [Range(0, 50, ErrorMessage = "Araç yaşı 0-50 arası olmalı.")]
    public int? AracYas { get; set; }

    [Display(Name = "Aracın Kilometresi")]
    [Required(ErrorMessage = "Araç kilometresi boş olamaz.")]
    [Range(0, 999999, ErrorMessage = "Kilometre 0-999.999 arası olmalı.")]
    public int? AracKm { get; set; }
}
