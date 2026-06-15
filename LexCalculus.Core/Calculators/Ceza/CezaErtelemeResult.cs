using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F1 Ceza Erteleme sonucu. TCK m.51 koşullarının her birinin sonucu, erteleme
/// bitiş tarihi (karar tarihi + süre) ve varsa denetimli serbestlik bitiş
/// tarihi raporlanır.
/// </summary>
public sealed class CezaErtelemeResult : CalculationResult
{
    public bool ErtelemeyeUygunMu { get; set; }
    public string? UygunsuzlukSebebi { get; set; }
    public DateOnly? ErtelemeBitisTarihi { get; set; }
    public DateOnly? DenetimliSerbestlikBitisTarihi { get; set; }

    /// <summary>Bu hesapta uygulanan üst sınır (yetişkin 730 / çocuk 1095 gün).</summary>
    public int UygulananUstSinirGun { get; set; }
}
