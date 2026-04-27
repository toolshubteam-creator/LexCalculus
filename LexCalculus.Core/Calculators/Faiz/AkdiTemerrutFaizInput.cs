using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class AkdiTemerrutFaizInput
{
    [Display(Name = "Ana Para (TL)")]
    [Required(ErrorMessage = "Ana para boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Ana para pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? AnaPara { get; set; }

    [Display(Name = "Temerrüt Tarihi (Borcun Muacceliyeti)")]
    [Required(ErrorMessage = "Temerrüt tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? TemerrutTarihi { get; set; }

    [Display(Name = "Hesap Tarihi")]
    [Required(ErrorMessage = "Hesap tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? HesapTarihi { get; set; }

    [Display(Name = "Sözleşme Dönemleri")]
    public List<SozlesmeOranDonem> SozlesmeOranlari { get; set; } = new();

    [Display(Name = "Faiz Yöntemi")]
    public FaizYontemi FaizYontemi { get; set; } = FaizYontemi.Basit;

    [Display(Name = "Bileşik Faiz Dönemi")]
    public BilesikDonemi BilesikDonemi { get; set; } = BilesikDonemi.Aylik;

    [Display(Name = "Gün/Yıl Bazı")]
    public GunYiliBazi GunYili { get; set; } = GunYiliBazi.UcYuzAltmisBes;

    [Display(Name = "Tacirler Arası Yazılı Sözleşme")]
    public bool TaclrlerArasiYaziliSozlesme { get; set; }
}
