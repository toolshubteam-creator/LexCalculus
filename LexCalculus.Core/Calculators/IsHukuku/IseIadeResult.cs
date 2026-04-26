using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class IseIadeResult : CalculationResult
{
    public bool IsGuvencesindeMi { get; set; }
    public int KidemAyi { get; set; }
    public int BostaGecenAy { get; set; }
    public int BostaSinirliAy { get; set; }
    public int IadeAyiSayisi { get; set; }
    public decimal IadeTazminati { get; set; }
    public decimal BostaGecenSureUcreti { get; set; }
    public decimal BrutToplam { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal NetTutar { get; set; }
}
