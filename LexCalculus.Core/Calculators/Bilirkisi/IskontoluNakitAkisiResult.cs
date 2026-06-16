using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I2 İskontolu Nakit Akışı sonucu. Bugünkü değer (PV) + girdi yansımaları.
/// </summary>
public sealed class IskontoluNakitAkisiResult : CalculationResult
{
    public decimal YillikNetGelir { get; set; }
    public decimal IskontoOraniYuzde { get; set; }
    public int YilSayisi { get; set; }
    public decimal BugunkuDeger { get; set; }
}
