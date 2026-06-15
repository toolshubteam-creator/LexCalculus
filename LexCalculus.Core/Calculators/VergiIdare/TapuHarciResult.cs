using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G2 Tapu Harcı sonucu. Alıcı/satıcı harçları ayrı + seçime göre toplam.
/// </summary>
public sealed class TapuHarciResult : CalculationResult
{
    public decimal HarcOrani { get; set; }
    public decimal SatisDegeri { get; set; }
    public decimal AliciHarc { get; set; }
    public decimal SaticiHarc { get; set; }
    public decimal ToplamHarc { get; set; }
}
