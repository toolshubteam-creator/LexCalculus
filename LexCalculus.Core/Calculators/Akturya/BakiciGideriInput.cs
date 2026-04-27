using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class BakiciGideriInput
{
    [Display(Name = "Olay Tarihi")]
    [Required(ErrorMessage = "Olay tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OlayTarihi { get; set; }

    [Display(Name = "Yaralanan Kişi Doğum Tarihi")]
    [Required(ErrorMessage = "Doğum tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? YaralananDogumTarihi { get; set; }

    [Display(Name = "Cinsiyet")]
    [Required]
    public Cinsiyet Cinsiyet { get; set; } = Cinsiyet.Erkek;

    [Display(Name = "Aylık Bakıcı Maliyeti (TL)")]
    [Required(ErrorMessage = "Aylık bakıcı maliyeti boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Maliyet pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AylikBakiciMaliyeti { get; set; }

    [Display(Name = "Bakım İhtiyaç Oranı (%)")]
    [Required(ErrorMessage = "Bakım ihtiyaç oranı boş olamaz.")]
    [Range(1, 100, ErrorMessage = "Bakım oranı %1-100 arası olmalıdır.")]
    public decimal? BakimIhtiyacOrani { get; set; } = 100m;

    [Display(Name = "Yıllık İskonto Oranı (% net)")]
    [Range(0.0, 20.0, ErrorMessage = "İskonto oranı 0-20% arası olmalı.")]
    public decimal YillikIskontoOrani { get; set; } = 3.0m;
}
