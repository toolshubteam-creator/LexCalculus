using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G4 KDV İadesi sonucu. İadeye konu (mahsup öncesi) ve mahsup sonrası net
/// iade tutarı ayrı raporlanır.
/// </summary>
public sealed class KdvIadesiResult : CalculationResult
{
    public decimal IadeyeKonuKDV { get; set; }
    public decimal MahsupSonrasiTutar { get; set; }
    public decimal IadeTutari { get; set; }
}
