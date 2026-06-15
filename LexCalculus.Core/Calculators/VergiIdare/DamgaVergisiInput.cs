using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// Damga vergisi belge türü (488 s.K. Tarifesi dispatch'i için). 2026
/// oranları FormulaParameter slug "damga-vergisi" üzerinden okunur.
/// </summary>
public enum DamgaBelgeTuru
{
    [Display(Name = "Genel Sözleşme (‰9,48)")]
    GenelSozlesme = 1,

    [Display(Name = "Kira Mukavelesi (‰1,89)")]
    KiraMukavelesi = 2,

    [Display(Name = "İhale Kararı (‰5,69)")]
    IhaleKarari = 3,

    [Display(Name = "Makbuz (‰9,48)")]
    Makbuz = 4,

    [Display(Name = "Diğer (‰9,48 — varsayılan)")]
    Diger = 5
}

/// <summary>
/// G3 Damga Vergisi — 488 s.K. Belge değeri × oran × adet; her belge için
/// azami sınır cap'i uygulanır (2026: 5.281.302,40 TL, 86 Seri No'lu Damga
/// Vergisi Genel Tebliği).
/// </summary>
public sealed class DamgaVergisiInput
{
    [Display(Name = "Belge Türü")]
    public DamgaBelgeTuru BelgeTuru { get; set; } = DamgaBelgeTuru.GenelSozlesme;

    [Display(Name = "Belge Değeri / Tutarı (TL)")]
    [Required(ErrorMessage = "Belge değeri boş olamaz.")]
    [Range(typeof(decimal), "0.01", "999999999999",
        ErrorMessage = "Belge değeri pozitif olmalıdır.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? DegerTutari { get; set; }

    [Display(Name = "Belge Adedi")]
    [Range(1, 10000, ErrorMessage = "Belge adedi 1-10000 arası olmalıdır.")]
    public int BelgeAdedi { get; set; } = 1;

    [Display(Name = "Hesap Tarihi (oran ve azami sınır referansı)")]
    [DataType(DataType.Date)]
    public DateTime? AsOfDate { get; set; }
}
