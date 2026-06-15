using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>Ceza türü (TCK m.51 erteleme limit dispatch için).</summary>
public enum CezaTuru
{
    [Display(Name = "Hapis Cezası (yetişkin) — limit 2 yıl")]
    HapisYetiskin = 1,

    [Display(Name = "Hapis Cezası (çocuk/18 yaş altı) — limit 3 yıl")]
    HapisCocuk = 2,

    [Display(Name = "Adli Para Cezası — erteleme dışı")]
    AdliPara = 3
}

/// <summary>Mahkemenin takdir ettiği erteleme süresi (TCK m.51/3 — 1-3 yıl).</summary>
public enum ErtelemeSuresi
{
    [Display(Name = "1 yıl (12 ay)")]
    BirYil = 12,

    [Display(Name = "2 yıl (24 ay)")]
    IkiYil = 24,

    [Display(Name = "3 yıl (36 ay)")]
    UcYil = 36
}

/// <summary>
/// F1 Ceza Erteleme — TCK m.51 koşullarını ve denetim süresini hesaplar.
/// Ceza türüne göre üst sınır (yetişkin 2 yıl / çocuk 3 yıl), adli sicil
/// temiz şartı ve mahkemenin takdir ettiği erteleme süresi kontrol edilir.
/// Output, sadece "TCK m.51 koşulları sağlanıyor mu" prensip kontrolüdür —
/// mahkemenin takdir yetkisi (m.51/1 son cümle) hesabın dışındadır.
/// </summary>
public sealed class CezaErtelemeInput
{
    [Display(Name = "Verilen Ceza (gün)")]
    [Required(ErrorMessage = "Ceza gün sayısı boş olamaz.")]
    [Range(1, 36500, ErrorMessage = "Ceza 1-36500 gün arası olmalıdır.")]
    public int? VerilenCezaGun { get; set; }

    [Display(Name = "Ceza Türü")]
    public CezaTuru CezaTuru { get; set; } = CezaTuru.HapisYetiskin;

    [Display(Name = "Erteleme Süresi")]
    public ErtelemeSuresi ErtelemeSuresi { get; set; } = ErtelemeSuresi.IkiYil;

    [Display(Name = "Karar Tarihi")]
    [Required(ErrorMessage = "Karar tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? KararTarihi { get; set; }

    [Display(Name = "Sanığın adli sicili temiz (önceki kasıtlı suçtan 3 ay+ ceza yok)")]
    public bool AdliSicilTemiz { get; set; } = true;

    [Display(Name = "Denetimli Serbestlik Süresi (ay) — opsiyonel")]
    [Range(0, 60, ErrorMessage = "Denetimli serbestlik 0-60 ay arası olmalıdır.")]
    public int? DenetimliSerbestlikAy { get; set; }
}
