using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>KDV iade başvuru türü (raporlama amacıyla; formül aynı).</summary>
public enum KdvIadeBasvuruTuru
{
    [Display(Name = "İndirimli Oran (m.29/2)")]
    IndirimliOran = 1,

    [Display(Name = "İhraç Kayıtlı / Tam İstisna (m.11)")]
    IhracKayitli = 2,

    [Display(Name = "Diğer İade Hâlleri")]
    DigerIade = 3
}

/// <summary>
/// G4 KDV İadesi — 3065 s.K. m.32. İndirilebilir KDV'nin hesaplanan KDV'yi
/// aşan kısmı iadeye konu; mahsup edilen kısım düşülür.
/// </summary>
public sealed class KdvIadesiInput
{
    [Display(Name = "Toplam Hesaplanan KDV (Satış, TL)")]
    [Required(ErrorMessage = "Hesaplanan KDV boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Hesaplanan KDV negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamHesaplananKDV { get; set; }

    [Display(Name = "Toplam İndirim KDV (Alış, TL)")]
    [Required(ErrorMessage = "İndirim KDV boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "İndirim KDV negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamIndirimKDV { get; set; }

    [Display(Name = "Mahsup Edilen KDV (TL) — opsiyonel")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Mahsup edilen KDV negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MahsupEdilenKDV { get; set; } = 0m;

    [Display(Name = "İade Başvuru Türü")]
    public KdvIadeBasvuruTuru IadeBasvuruTuru { get; set; } = KdvIadeBasvuruTuru.IndirimliOran;

    [Display(Name = "Hesap Tarihi (referans)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
