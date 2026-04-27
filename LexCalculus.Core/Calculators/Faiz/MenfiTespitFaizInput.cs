using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class MenfiTespitFaizInput
{
    [Display(Name = "Haksız Tahsil Edilen Tutar (TL)")]
    [Required(ErrorMessage = "Haksız tahsil tutarı boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Tutar pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? HaksizTahsilTutari { get; set; }

    [Display(Name = "Tahsil / Ödeme Tarihi")]
    [Required(ErrorMessage = "Tahsil tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? TahsilTarihi { get; set; }

    [Display(Name = "İade Talep Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? IadeTalepTarihi { get; set; }

    [Display(Name = "Hesap Tarihi")]
    [Required(ErrorMessage = "Hesap tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? HesapTarihi { get; set; }

    [Display(Name = "Alacaklı Kötüniyetli mi?")]
    public bool AlacakliKotuNiyetli { get; set; } = true;

    [Display(Name = "Gün/Yıl Bazı")]
    public GunYiliBazi GunYili { get; set; } = GunYiliBazi.UcYuzAltmisBes;
}
