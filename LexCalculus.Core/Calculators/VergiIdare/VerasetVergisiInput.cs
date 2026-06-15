using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// İntikal türü (7338 s.K. m.16) — dilim seti dispatch'i ve uygulanacak
/// istisna tutarı bu enuma göre belirlenir.
/// </summary>
public enum IntikalTuru
{
    [Display(Name = "Veraset (füruğ veya eş varsa — kişi başı istisna)")]
    VerasetFurugVeEs = 1,

    [Display(Name = "Veraset (yalnız eş, füruğ yok — yüksek istisna)")]
    VerasetSadeceEs = 2,

    [Display(Name = "İvazsız İntikal / Bağış")]
    Ivazsiz = 3
}

/// <summary>
/// G1 Veraset ve İntikal Vergisi — 7338 s.K. m.4 + m.16. Brüt değerden
/// istisna düşülür, kalan vergilendirilebilir tutara dilim hesabı uygulanır
/// (<see cref="LexCalculus.Core.Services.ITaxBracketService"/>).
/// </summary>
public sealed class VerasetVergisiInput
{
    [Display(Name = "İntikal Türü")]
    public IntikalTuru IntikalTuru { get; set; } = IntikalTuru.VerasetFurugVeEs;

    [Display(Name = "Brüt İntikal Değeri (TL)")]
    [Required(ErrorMessage = "Brüt intikal değeri boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Brüt intikal değeri pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? BrutDeger { get; set; }

    /// <summary>
    /// Mirasçı sayısı. Veraset hâlinde istisna KİŞİ BAŞINA uygulanır
    /// (m.4/d). İvazsız hâlinde mirasçı sayısı anlamsızdır; 1 alınır.
    /// </summary>
    [Display(Name = "Mirasçı Sayısı (veraset hâlinde kişi başı istisna)")]
    [Range(1, 100, ErrorMessage = "Mirasçı sayısı 1-100 arası olmalıdır.")]
    public int MirascıSayisi { get; set; } = 1;

    [Display(Name = "Hesap Tarihi (tarife referansı)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
