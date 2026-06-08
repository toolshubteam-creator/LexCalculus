using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>Nafaka türü (TMK).</summary>
public enum NafakaTuru
{
    [Display(Name = "İştirak Nafakası (çocuk için)")]
    Istirak = 1,

    [Display(Name = "Yoksulluk Nafakası (eş için)")]
    Yoksulluk = 2,

    [Display(Name = "Tedbir Nafakası (dava süresince)")]
    Tedbir = 3
}

/// <summary>Yeni belirleme mi, mevcut nafakanın artışı mı?</summary>
public enum NafakaHesapTuru
{
    [Display(Name = "Yeni Belirleme")]
    YeniHesap = 1,

    [Display(Name = "Artış (mevcut nafaka)")]
    Artis = 2
}

/// <summary>Çocuğun eğitim seviyesi (iştirak nafakası katsayısı).</summary>
public enum EgitimSeviyesi
{
    [Display(Name = "Okul Öncesi / Anaokulu")]
    Anaokul = 1,

    [Display(Name = "İlkokul")]
    Ilkokul = 2,

    [Display(Name = "Ortaokul")]
    Ortaokul = 3,

    [Display(Name = "Lise")]
    Lise = 4,

    [Display(Name = "Üniversite")]
    Universite = 5
}

/// <summary>İkamet edilen yer tipi (iştirak nafakası katsayısı).</summary>
public enum SehirTuru
{
    [Display(Name = "Büyükşehir")]
    Buyuksehir = 1,

    [Display(Name = "Diğer")]
    Diger = 2
}

/// <summary>İştirak nafakası için tek çocuğun bilgileri.</summary>
public sealed class NafakaCocukGirdi
{
    [Display(Name = "Yaş")]
    [Range(0, 17, ErrorMessage = "Çocuk yaşı 0-17 arası olmalıdır.")]
    public int Yas { get; set; }

    [Display(Name = "Eğitim Seviyesi")]
    public EgitimSeviyesi EgitimSeviyesi { get; set; } = EgitimSeviyesi.Anaokul;
}

/// <summary>
/// Inputs for the nafaka (alimony / child support) calculator. A single calculator
/// dispatches on <see cref="NafakaTuru"/> (iştirak / yoksulluk / tedbir) and
/// <see cref="NafakaHesapTuru"/> (yeni belirleme / artış). Per-mode rules are
/// validated inside the calculator. Legal basis: TMK m.169, m.175, m.182, m.197,
/// m.364.
/// </summary>
public sealed class NafakaInput
{
    [Display(Name = "Nafaka Türü")]
    public NafakaTuru NafakaTuru { get; set; } = NafakaTuru.Istirak;

    [Display(Name = "Hesap Türü")]
    public NafakaHesapTuru HesapTuru { get; set; } = NafakaHesapTuru.YeniHesap;

    // ----- İştirak (yeni belirleme) -----
    [Display(Name = "Yükümlü Net Aylık Geliri (TL)")]
    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Yükümlü geliri negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YukumluNetGelir { get; set; }

    [Display(Name = "İkamet")]
    public SehirTuru Sehir { get; set; } = SehirTuru.Diger;

    public List<NafakaCocukGirdi> Cocuklar { get; set; } = new();

    // ----- Yoksulluk / Tedbir (yeni belirleme) -----
    [Display(Name = "Yüksek Gelirli Eşin Aylık Geliri (TL)")]
    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Gelir negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? YuksekGelirEs { get; set; }

    [Display(Name = "Düşük Gelirli Eşin Aylık Geliri (TL)")]
    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Gelir negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? DusukGelirEs { get; set; }

    [Display(Name = "Evlilik Süresi (ay)")]
    [Range(0, 1200, ErrorMessage = "Evlilik süresi 0-1200 ay arası olmalıdır.")]
    public int? EvlilikSuresiAy { get; set; }

    // ----- Artış (mevcut nafaka) -----
    [Display(Name = "Mevcut Aylık Nafaka (TL)")]
    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Mevcut nafaka negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MevcutAylikNafaka { get; set; }

    [Display(Name = "Artış Hesap Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? HesapTarihi { get; set; }

    /// <summary>Sistemde olmayan ay için manuel TÜFE 12 aylık ortalama girişi (artış).</summary>
    [Display(Name = "TÜFE Override (% — opsiyonel)")]
    [Range(typeof(decimal), "0", "1000",
        ErrorMessage = "TÜFE oranı 0-1000 arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? TufeOverride { get; set; }

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
