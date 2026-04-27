using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class AracDegerKaybiResult : CalculationResult
{
    public decimal HasarTutari { get; set; }
    public decimal HasarOrani { get; set; }
    public decimal YasFaktoru { get; set; }
    public decimal KmFaktoru { get; set; }
    public decimal DegerKaybi { get; set; }
    public bool PertRiski { get; set; }
}
