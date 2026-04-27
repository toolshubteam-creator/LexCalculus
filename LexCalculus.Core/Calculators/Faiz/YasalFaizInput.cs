using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class YasalFaizInput
{
    [Display(Name = "Ana Para (TL)")]
    [Required(ErrorMessage = "Ana para boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Ana para pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AnaPara { get; set; }

    [Display(Name = "Başlangıç Tarihi (Temerrüt / Muacceliyet)")]
    [Required(ErrorMessage = "Başlangıç tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? BaslangicTarihi { get; set; }

    [Display(Name = "Hesap Tarihi")]
    [Required(ErrorMessage = "Hesap tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? HesapTarihi { get; set; }

    [Display(Name = "Gün/Yıl Bazı")]
    public GunYiliBazi GunYili { get; set; } = GunYiliBazi.UcYuzAltmisBes;
}
