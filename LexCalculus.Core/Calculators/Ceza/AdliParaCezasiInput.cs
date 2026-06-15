using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>Adli para cezası hesap türü (TCK m.52 dispatch için).</summary>
public enum AdliParaHesapTuru
{
    [Display(Name = "Direkt: gün sayısı × günlük miktar")]
    Direkt = 1,

    [Display(Name = "Hapis cezasının adli paraya çevrimi")]
    HapisCevrim = 2
}

/// <summary>
/// F4 Adli Para Cezası — TCK m.52. Direkt hesap (gün sayısı × günlük miktar) ya
/// da hapis cezasının adli paraya çevrimi. Günlük miktar mahkeme takdir
/// kapsamındadır; TCK m.52/2 sınırları (20-100 TL bandı) kanunda yazılı
/// rakamlar değil, idari tebliğlerle güncellenir — burada sınır mevzuat sabiti
/// olarak alındı.
/// </summary>
public sealed class AdliParaCezasiInput
{
    [Display(Name = "Hesap Türü")]
    public AdliParaHesapTuru HesapTuru { get; set; } = AdliParaHesapTuru.Direkt;

    // ----- Direkt -----
    [Display(Name = "Gün Sayısı (5-730)")]
    [Range(5, 730, ErrorMessage = "Gün sayısı 5-730 arası olmalıdır.")]
    public int? GunSayisi { get; set; }

    [Display(Name = "Günlük Miktar (TL, 20-100)")]
    [Range(typeof(decimal), "20", "100",
        ErrorMessage = "Günlük miktar 20-100 TL arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? GunlukMiktar { get; set; }

    // ----- Hapis çevrim -----
    [Display(Name = "Hapis Cezası (gün)")]
    [Range(1, 36500, ErrorMessage = "Hapis günü 1-36500 arası olmalıdır.")]
    public int? HapisGun { get; set; }

    [Display(Name = "Çevrim Günlük Miktar (TL, 20-100)")]
    [Range(typeof(decimal), "20", "100",
        ErrorMessage = "Çevrim günlük miktar 20-100 TL arası olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? CevrimGunlukMiktar { get; set; }
}
