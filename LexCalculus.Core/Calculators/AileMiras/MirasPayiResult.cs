using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.AileMiras;

public sealed class MirasPayiSatiri
{
    public required string Tanim { get; init; }
    public required string MirasciTuru { get; init; }
    public required decimal PayKesri { get; init; }
    public required decimal? PayTutari { get; init; }
}

/// <summary>
/// Legal inheritance share result. Inherits the standard envelope and adds the
/// per-heir share list and the active zümre (derece).
/// </summary>
public sealed class MirasPayiResult : CalculationResult
{
    public List<MirasPayiSatiri> PayListesi { get; set; } = new();
    public int AktifDerece { get; set; }
}
