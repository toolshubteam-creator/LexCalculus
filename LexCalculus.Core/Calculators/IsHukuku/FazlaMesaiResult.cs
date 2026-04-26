using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class FazlaMesaiResult : CalculationResult
{
    public decimal SaatlikUcret { get; set; }
    public int FazlaMesaiSaati { get; set; }
    public int HaftaTatiliSaati { get; set; }
    public int BayramSaati { get; set; }
    public decimal FazlaMesaiTutari { get; set; }
    public decimal HaftaTatiliTutari { get; set; }
    public decimal BayramTutari { get; set; }
    public decimal BrutToplam { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal NetTutar { get; set; }
}
