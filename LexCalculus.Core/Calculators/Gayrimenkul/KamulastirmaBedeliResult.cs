using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Expropriation value result. Inherits the standard envelope and adds the
/// land/building split plus the objective-increase audit fields (whether the
/// %100 ceiling from Yargıtay 5. HD K. 2005/675 was applied).
/// </summary>
public sealed class KamulastirmaBedeliResult : CalculationResult
{
    public decimal ArsaBedeli { get; set; }
    public decimal BinaBedeli { get; set; }
    public decimal ToplamBedel { get; set; }

    public string KullanilanYontem { get; set; } = string.Empty;

    /// <summary>The objective-increase ratio actually applied (after the ceiling cap), as a fraction.</summary>
    public decimal ObjektifArtisUygulanan { get; set; }

    /// <summary>True if the requested objective-increase ratio exceeded the legal ceiling and was capped.</summary>
    public bool ObjektifArtisCapllendi { get; set; }
}
