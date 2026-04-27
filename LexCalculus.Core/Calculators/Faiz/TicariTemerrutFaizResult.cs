using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Faiz;

public sealed class TicariTemerrutFaizResult : CalculationResult
{
    public decimal AnaPara { get; set; }
    public int ToplamGun { get; set; }

    public decimal YasalFaizTutari { get; set; }
    public decimal YasalToplamTutar { get; set; }
    public List<FaizDonemDetay> YasalDonemDetaylar { get; set; } = new();

    public decimal TicariFaizTutari { get; set; }
    public decimal TicariToplamTutar { get; set; }
    public List<FaizDonemDetay> TicariDonemDetaylar { get; set; } = new();

    public string OnerilenSecim { get; set; } = string.Empty;
    public decimal OnerilenTutar { get; set; }
}
