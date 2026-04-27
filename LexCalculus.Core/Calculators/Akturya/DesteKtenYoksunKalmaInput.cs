using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class DesteKtenYoksunKalmaInput
{
    [Display(Name = "Olay Tarihi")]
    [Required(ErrorMessage = "Olay tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OlayTarihi { get; set; }

    [Display(Name = "Ölen Kişi Doğum Tarihi")]
    [Required(ErrorMessage = "Doğum tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? OlenDogumTarihi { get; set; }

    [Display(Name = "Ölen Kişi Cinsiyeti")]
    [Required]
    public Cinsiyet OlenCinsiyet { get; set; } = Cinsiyet.Erkek;

    [Display(Name = "Aylık Net Geliri (TL)")]
    [Required(ErrorMessage = "Gelir boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Gelir pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AylikGelir { get; set; }

    [Display(Name = "Eşi Var Mı")]
    public bool EsVarMi { get; set; }

    [Display(Name = "Eş Doğum Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? EsDogumTarihi { get; set; }

    [Display(Name = "Eş Cinsiyeti")]
    public Cinsiyet EsCinsiyet { get; set; } = Cinsiyet.Kadin;

    [Display(Name = "Çocuklar")]
    public List<CocukInput> Cocuklar { get; set; } = new();

    [Display(Name = "Yıllık İskonto Oranı (% net)")]
    [Range(0.0, 20.0, ErrorMessage = "İskonto oranı 0-20% arası olmalı.")]
    public decimal YillikIskontoOrani { get; set; } = 3.0m;

    [Display(Name = "Pasif Dönem Gelir Oranı (%)")]
    [Range(20, 100, ErrorMessage = "Pasif dönem oranı 20-100% arası olmalı.")]
    public int PasifDonemGelirOrani { get; set; } = 50;
}
