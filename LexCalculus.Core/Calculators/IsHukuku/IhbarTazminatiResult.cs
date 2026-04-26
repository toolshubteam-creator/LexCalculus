using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class IhbarTazminatiResult : CalculationResult
{
    public int ToplamGun { get; set; }
    public int IhbarHaftasi { get; set; }
    public int IhbarGunu { get; set; }
    public decimal GunlukUcret { get; set; }
    public decimal BrutIhbar { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal NetIhbar { get; set; }
    public IhbarHakEden HakEden { get; set; }
}
