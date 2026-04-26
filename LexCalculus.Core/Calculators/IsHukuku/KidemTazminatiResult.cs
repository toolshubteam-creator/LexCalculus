using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

/// <summary>
/// Severance pay calculation breakdown. Inherits standard envelope (TotalAmount,
/// Rows, Note, Warnings) and adds typed fields for the audit trail used by
/// CalculationHistory persistence in Phase 2.22.
/// </summary>
public sealed class KidemTazminatiResult : CalculationResult
{
    public int ToplamGun { get; set; }
    public int TamYil { get; set; }
    public int KalanGun { get; set; }
    public decimal GiydirilmisUcret { get; set; }
    public decimal KullanilanTavan { get; set; }
    public bool TavanAsildi { get; set; }
    public decimal BrutKidem { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal NetKidem { get; set; }
    public decimal? IhbarTazminati { get; set; }
    public int? IhbarHaftasi { get; set; }
}
