using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I4 Çevresel Zarar sonucu. Kalem dökümü + opsiyonel bilirkişi maliyet +
/// toplam tazminat.
/// </summary>
public sealed class CevreselZararResult : CalculationResult
{
    public decimal DogrudanZarar { get; set; }
    public decimal RestorasyonMaliyeti { get; set; }
    public decimal EkosistemKaybi { get; set; }
    public decimal ToplamZarar { get; set; }
    public decimal BilirkisiMaliyet { get; set; }
    public decimal ToplamTazminat { get; set; }
}
