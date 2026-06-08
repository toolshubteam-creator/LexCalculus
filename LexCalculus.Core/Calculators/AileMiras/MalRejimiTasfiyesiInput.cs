using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Inputs for the marital property regime liquidation calculator
/// (Edinilmiş Mallara Katılma Rejimi Tasfiyesi — TMK m.218-241).
///
/// Only acquired property (edinilmiş mal) net of related debts forms the
/// "artık değer". Personal property (kişisel mal: pre-marriage assets,
/// inheritance, gifts — TMK m.220) is EXCLUDED from liquidation; it is captured
/// here only to be reported, never added to the artık değer. Domain rules are
/// validated inside the calculator.
/// </summary>
public sealed class MalRejimiTasfiyesiInput
{
    // ----- Eş 1 -----
    [Display(Name = "Eş 1 — Evlilik Öncesi Mal (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Evlilik öncesi mal negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es1EvlilikOncesiMal { get; set; }

    [Display(Name = "Eş 1 — Evlilik İçinde Edinilen Mal (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Edinilen mal negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es1EdinilenMal { get; set; }

    [Display(Name = "Eş 1 — Edinilen Mala İlişkin Borç (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Borç negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es1Borc { get; set; }

    [Display(Name = "Eş 1 — Miras / Bağış ile Edinilen (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Miras/bağış değeri negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es1MirasBagis { get; set; }

    // ----- Eş 2 -----
    [Display(Name = "Eş 2 — Evlilik Öncesi Mal (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Evlilik öncesi mal negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es2EvlilikOncesiMal { get; set; }

    [Display(Name = "Eş 2 — Evlilik İçinde Edinilen Mal (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Edinilen mal negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es2EdinilenMal { get; set; }

    [Display(Name = "Eş 2 — Edinilen Mala İlişkin Borç (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Borç negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es2Borc { get; set; }

    [Display(Name = "Eş 2 — Miras / Bağış ile Edinilen (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Miras/bağış değeri negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Es2MirasBagis { get; set; }

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
