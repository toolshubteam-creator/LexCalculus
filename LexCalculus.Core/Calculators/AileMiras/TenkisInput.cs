using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>Tek bir sağlar arası bağış (tenkis için geri eklenir, TMK m.565).</summary>
public sealed class BagisGirdi
{
    [Display(Name = "Bağış Tarihi")]
    [DataType(DataType.Date)]
    public DateTime? Tarih { get; set; }

    [Display(Name = "Bağış Tutarı (TL)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Bağış tutarı negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? Tutar { get; set; }

    [Display(Name = "Alıcı")]
    public string? AliciTanim { get; set; }
}

/// <summary>
/// Inputs for the tenkis (abatement) calculator (TMK m.506 saklı pay +
/// m.560-571 tenkis). Shares the heir structure with E2; adds the will amount
/// and the inter-vivos gifts that are added back to the estate (m.565).
/// </summary>
public sealed class TenkisInput
{
    [Display(Name = "Ölüm Anı Net Malvarlığı (TL)")]
    [Required(ErrorMessage = "Malvarlığı boş olamaz.")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Malvarlığı negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? ToplamMalvarligi { get; set; }

    public MirasciYapisiInput Yapi { get; set; } = new();

    [Display(Name = "Vasiyetname Tutarı (TL, opsiyonel)")]
    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "Vasiyet tutarı negatif olamaz.",
        ParseLimitsInInvariantCulture = true)]
    public decimal? VasiyetnameTutari { get; set; }

    public List<BagisGirdi> Bagislar { get; set; } = new();

    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
