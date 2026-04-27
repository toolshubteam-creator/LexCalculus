using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class BakiciGideriResult : CalculationResult
{
    public int YaralananYas { get; set; }
    public decimal BekledigiYasam { get; set; }
    public int DestekSuresiYil { get; set; }
    public decimal AylikMaliyet { get; set; }
    public decimal AylikEffektifMaliyet { get; set; }
    public decimal YillikMaliyet { get; set; }
    public decimal ToplamTazminat { get; set; }
}
