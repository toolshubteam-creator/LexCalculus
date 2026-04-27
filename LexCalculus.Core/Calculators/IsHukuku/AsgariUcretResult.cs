using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class AsgariUcretResult : CalculationResult
{
    public int ToplamAy { get; set; }
    public int EksikAy { get; set; }
    public decimal ToplamEksikBrut { get; set; }
    public decimal YasalFaiz { get; set; }
    public decimal ToplamAlacak { get; set; }
    public List<AylikDetay> AylikDetaylar { get; set; } = new();
}

public sealed class AylikDetay
{
    public required DateTime Ay { get; init; }
    public decimal AsgariBrut { get; init; }
    public decimal OdenenBrut { get; init; }
    public decimal Eksiklik { get; init; }
}
