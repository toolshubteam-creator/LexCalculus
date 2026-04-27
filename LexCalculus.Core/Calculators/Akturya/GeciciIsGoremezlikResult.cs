using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Akturya;

public sealed class GeciciIsGoremezlikResult : CalculationResult
{
    public decimal GunlukBrut { get; set; }
    public int SureGun { get; set; }
    public decimal BrutMahrumTutar { get; set; }
    public decimal SgkOdenegi { get; set; }
    public decimal NetMahrumTutar { get; set; }
}
