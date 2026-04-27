using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class MaluliyetResult : CalculationResult
{
    public int YaralananYas { get; set; }
    public decimal BekledigiYasam { get; set; }
    public int AktifYil { get; set; }
    public int PasifYil { get; set; }
    public decimal YillikGelir { get; set; }
    public decimal YillikKayip { get; set; }
    public decimal AktifPV { get; set; }
    public decimal PasifPV { get; set; }
    public decimal ToplamTazminat { get; set; }
}
