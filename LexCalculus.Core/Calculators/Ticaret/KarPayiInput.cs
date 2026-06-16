using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>
/// H2 Anonim Şirket Kâr Payı — TTK m.508 + m.519. Net kâr üzerinden sırayla:
/// (1) yasal yedek (m.519/1, %5 — sermaye %20 cap),
/// (2) birinci temettü (m.508/1, sermaye × %5 asgari),
/// (3) opsiyonel özel yedek (m.519/2, kalan × %10),
/// (4) ikinci temettü (m.508/2, kalan).
/// </summary>
public sealed class KarPayiInput
{
    [Display(Name = "Net Kâr (TL)")]
    [Required(ErrorMessage = "Net kâr boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Net kâr pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? NetKar { get; set; }

    [Display(Name = "Kanuni Sermaye (TL)")]
    [Required(ErrorMessage = "Kanuni sermaye boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Kanuni sermaye pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? KanuniSermaye { get; set; }

    [Display(Name = "Mevcut Yasal Yedek (TL) — birikmiş")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Mevcut yasal yedek negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? MevcutYasalYedek { get; set; } = 0m;

    [Display(Name = "Özel Yedek Uygulanır (m.519/2 esas sözleşme şartı)")]
    public bool OzelYedekUygulanir { get; set; }
}
