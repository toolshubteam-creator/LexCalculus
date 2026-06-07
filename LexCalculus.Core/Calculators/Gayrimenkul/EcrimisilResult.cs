using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>One yearly segment of the occupation, with the ÜFE-escalated rent applied to it.</summary>
public sealed class EcrimisilDonem
{
    public required int Yil { get; init; }
    public required DateTime BaslangicTarih { get; init; }
    public required DateTime BitisTarih { get; init; }
    public required int AySayisi { get; init; }
    public required decimal UfeOrani { get; init; }        // o yıl uygulanan ÜFE artış oranı (yüzde)
    public required decimal GuncelAylikKira { get; init; }
    public required decimal DonemBedeli { get; init; }
}

/// <summary>
/// Ecrimisil result. Inherits the standard envelope and adds the per-year
/// breakdown showing how each period's rent was escalated by ÜFE.
/// </summary>
public sealed class EcrimisilResult : CalculationResult
{
    public IReadOnlyList<EcrimisilDonem> DonemListesi { get; set; } = new List<EcrimisilDonem>();
    public decimal ToplamEcrimisil { get; set; }
}
