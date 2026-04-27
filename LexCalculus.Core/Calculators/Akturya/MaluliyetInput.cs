using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class MaluliyetInput
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

    [Display(Name = "Aylık Net Geliri (TL)")]
    [Required(ErrorMessage = "Gelir boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Gelir pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AylikGelir { get; set; }

    [Display(Name = "İş Gücü Kaybı Oranı (%)")]
    [Required(ErrorMessage = "İş gücü kaybı oranı boş olamaz.")]
    [Range(0.1, 100.0, ErrorMessage = "Kayıp oranı %0.1-100 arası olmalı.")]
    public decimal? IsGucuKaybiOrani { get; set; }

    [Display(Name = "Yıllık İskonto Oranı (% net)")]
    [Range(0.0, 20.0, ErrorMessage = "İskonto oranı 0-20% arası olmalı.")]
    public decimal YillikIskontoOrani { get; set; } = 3.0m;

    [Display(Name = "Pasif Dönem Gelir Oranı (%)")]
    [Range(20, 100, ErrorMessage = "Pasif dönem oranı 20-100% arası olmalı.")]
    public int PasifDonemGelirOrani { get; set; } = 50;
}
