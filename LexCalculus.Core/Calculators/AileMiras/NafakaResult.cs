using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>
/// Nafaka result. Inherits the standard envelope and adds the recommended monthly
/// amount plus the breakdown context (which türü/hesap, whether the asgari ücret
/// floor or TÜFE increase applied).
/// </summary>
public sealed class NafakaResult : CalculationResult
{
    public decimal OnerilenAylikNafaka { get; set; }

    /// <summary>İştirak: asgari ücret %25 alt sınırı en az bir çocukta devreye girdi mi.</summary>
    public bool MinimumUygulandi { get; set; }

    /// <summary>Artış: uygulanan TÜFE 12 aylık ortalama (%).</summary>
    public decimal? UygulananTufeOrani { get; set; }
}
