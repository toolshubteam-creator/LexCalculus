using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Per-unit land share breakdown. Weighted value = floor area × usage
/// coefficient × floor coefficient; Pay1000 is that unit's proportional share
/// expressed out of 1000 (the customary denominator for arsa payı).
/// </summary>
public sealed class BagimsizBolumPay
{
    public required string Tanim { get; init; }
    public required decimal Yuzolcumu { get; init; }
    public required KullanimTuru KullanimTuru { get; init; }
    public required int KatNumarasi { get; init; }
    public required decimal AgirliklikDeger { get; init; }
    public required decimal Pay1000 { get; init; }
}

/// <summary>
/// Arsa payı calculation result. Inherits the standard envelope (TotalAmount,
/// Rows, Note, Warnings, ValidationErrors) and adds the typed per-unit share
/// list for the result-panel detail table and CalculationHistory audit trail.
/// </summary>
public sealed class ArsaPayiResult : CalculationResult
{
    public IReadOnlyList<BagimsizBolumPay> Paylar { get; set; } = new List<BagimsizBolumPay>();
    public decimal ToplamAgirliklikDeger { get; set; }
    public decimal ToplamPay { get; set; }
}
