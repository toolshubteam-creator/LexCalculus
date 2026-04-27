using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class YasalFaizResult : CalculationResult
{
    public decimal AnaPara { get; set; }
    public int ToplamGun { get; set; }
    public decimal ToplamFaiz { get; set; }
    public decimal ToplamTutar { get; set; }
    public List<FaizDonemDetay> DonemDetaylar { get; set; } = new();
}

public sealed class FaizDonemDetay
{
    public required DateTime Baslangic { get; init; }
    public required DateTime Bitis { get; init; }
    public required int Gun { get; init; }
    public required decimal YillikOran { get; init; }
    public required decimal FaizTutari { get; init; }
}
