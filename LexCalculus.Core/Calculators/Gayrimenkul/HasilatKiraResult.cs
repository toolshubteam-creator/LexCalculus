using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>Which rule determined the final rent.</summary>
public enum HasilatKiraKurali
{
    /// <summary>Rent = ciro × oran (no floor/cap kicked in).</summary>
    CiroBazli = 1,

    /// <summary>Ciro-based rent was below the minimum guarantee; the minimum was used.</summary>
    MinimumGuvence = 2,

    /// <summary>Ciro-based rent exceeded the maximum cap; the cap was used.</summary>
    MaksimumTavan = 3
}

/// <summary>
/// Hâsılat (turnover) rent result. Inherits the standard envelope and adds the
/// raw ciro-based rent, the clamped payable rent, and which rule applied.
/// </summary>
public sealed class HasilatKiraResult : CalculationResult
{
    public decimal HesaplananKira { get; set; }
    public decimal OdenecekKira { get; set; }
    public HasilatKiraKurali HangiKuralDevreyeGirdi { get; set; }
}
