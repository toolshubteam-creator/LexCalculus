using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.IsHukuku;

public sealed class YillikIzinResult : CalculationResult
{
    public int ToplamGun { get; set; }
    public int TamYil { get; set; }
    public int YillikIzinGunHakki { get; set; }
    public int ToplamHakEdilenIzin { get; set; }
    public int KullanilanIzin { get; set; }
    public int KullanilmayanIzin { get; set; }
    public decimal GunlukUcret { get; set; }
    public decimal BrutIzinUcreti { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal GelirVergisi { get; set; }
    public decimal NetIzinUcreti { get; set; }
    public bool YasOzelHukum { get; set; }
}
