using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// Suç ağırlığı kategorileri (TCK m.66 asli zamanaşımı dispatch için).
/// Süreler: 8 / 15 / 20 / 25 / 30 yıl.
/// </summary>
public enum SucAgirligi
{
    [Display(Name = "5 yıldan az hapis veya adli para — asli 8 yıl")]
    Kisa = 1,

    [Display(Name = "5-20 yıl arası hapis — asli 15 yıl")]
    Orta = 2,

    [Display(Name = "20+ yıl hapis — asli 20 yıl")]
    Uzun = 3,

    [Display(Name = "Müebbet hapis — asli 25 yıl")]
    Muebbet = 4,

    [Display(Name = "Ağırlaştırılmış müebbet — asli 30 yıl")]
    AgirlastirilmisMuebbet = 5
}

/// <summary>
/// F3 Dava Zamanaşımı — TCK m.66 asli + m.67 kesinti + m.67/4 mutlak sınır.
/// Kesinti tarihleri zamanaşımını sıfırdan başlatır; ancak mutlak sınır
/// (suç işleme tarihi + asli × 1.5) hiçbir hâlde aşılamaz.
/// </summary>
public sealed class DavaZamanasimiInput
{
    [Display(Name = "Suç İşleme Tarihi")]
    [Required(ErrorMessage = "Suç işleme tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? SucIslemeTarihi { get; set; }

    [Display(Name = "Suç Ağırlığı (verilen / öngörülen ceza)")]
    public SucAgirligi SucAgirligi { get; set; } = SucAgirligi.Orta;

    /// <summary>
    /// TCK m.67 kapsamında zamanaşımını kesen yargılama işlemlerinin tarihleri
    /// (savcılık ifadesi, iddianame, yakalama emri, vs.). Boş bırakılabilir.
    /// </summary>
    public List<KesintiGirdi> Kesintiler { get; set; } = new();

    /// <summary>Zamanaşımı bitiş kontrol tarihi. Null → bugün.</summary>
    [Display(Name = "Hesap Tarihi (referans)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}

/// <summary>TCK m.67 kesinti işlemi (tarih + opsiyonel açıklama).</summary>
public sealed class KesintiGirdi
{
    [Display(Name = "Kesinti Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? Tarih { get; set; }

    [Display(Name = "İşlem Türü")]
    public string? IslemTuru { get; set; }
}
