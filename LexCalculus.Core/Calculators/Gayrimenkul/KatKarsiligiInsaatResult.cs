using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Gayrimenkul;

/// <summary>
/// Kat karşılığı share result. Inherits the standard envelope and adds the
/// owner/contractor split plus the (advisory) approximate unit counts.
/// </summary>
public sealed class KatKarsiligiInsaatResult : CalculationResult
{
    public decimal ArsaSahibiPay { get; set; }
    public decimal MuteahhitPay { get; set; }

    /// <summary>The owner ratio actually applied, as a fraction (0..1).</summary>
    public decimal ArsaOrani { get; set; }

    public decimal YaklasikArsaSahibiBolumSayisi { get; set; }
    public decimal YaklasikMuteahhitBolumSayisi { get; set; }
}
