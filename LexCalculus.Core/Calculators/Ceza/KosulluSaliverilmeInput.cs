using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// Koşullu salıverilme oran dispatch için suç tipi (5275 s.K. m.107).
/// Genel: 2/3 (~%66.67); diğer kategoriler: 3/4 (%75).
/// </summary>
public enum SucTipi
{
    [Display(Name = "Genel Suçlar (oran 2/3)")]
    Genel = 1,

    [Display(Name = "Terör Suçları (oran 3/4)")]
    Teror = 2,

    [Display(Name = "Cinsel Suçlar — TCK m.102/103/104/105 (oran 3/4)")]
    CinselSuc = 3,

    [Display(Name = "Örgütlü Suçlar (oran 3/4)")]
    OrgutluSuc = 4,

    [Display(Name = "Diğer Ağır Suçlar — kasten öldürme, vb. (oran 3/4)")]
    DigerAgirSuc = 5
}

/// <summary>
/// F2 Koşullu Salıverilme — 5275 s.K. m.107. Mahkumiyet süresi ve suç tipine
/// göre koşullu tahliye tarihini hesaplar; tutukluluk mahsubu net infaz
/// süresinden düşülür (m.107/9 referansı, asıl m.63 TCK).
/// </summary>
public sealed class KosulluSaliverilmeInput
{
    [Display(Name = "Mahkumiyet Süresi (gün)")]
    [Required(ErrorMessage = "Mahkumiyet süresi boş olamaz.")]
    [Range(1, 36500, ErrorMessage = "Mahkumiyet 1-36500 gün arası olmalıdır.")]
    public int? MahkumiyetGun { get; set; }

    [Display(Name = "Cezaevine Giriş Tarihi")]
    [Required(ErrorMessage = "Cezaevine giriş tarihi boş olamaz.")]
    [DataType(DataType.Date)]
    public DateTime? CezaevineGirisTarihi { get; set; }

    [Display(Name = "Suç Tipi")]
    public SucTipi SucTipi { get; set; } = SucTipi.Genel;

    [Display(Name = "Tutukluluk Süresi (gün) — mahsup için")]
    [Range(0, 36500, ErrorMessage = "Tutukluluk 0-36500 gün arası olmalıdır.")]
    public int? TutuklulukGun { get; set; } = 0;

    /// <summary>Kalan gün sayısının hesaplandığı referans tarih.</summary>
    [Display(Name = "Hesap Tarihi (kalan gün için referans)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
