using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F2 Koşullu Salıverilme sonucu. Şartlı tahliye tarihi, kalan gün sayısı (eksi
/// olabilir = geçmiş), suç tipine göre uygulanan oran ve net infaz süresi
/// (tutukluluk mahsubu sonrası) raporlanır.
/// </summary>
public sealed class KosulluSaliverilmeResult : CalculationResult
{
    public DateOnly? SartliTahliyeTarihi { get; set; }
    public int KalanGunSayisi { get; set; }
    public decimal HesaplananOran { get; set; }
    public int NetInfazSuresi { get; set; }
    public string? UyariMesaji { get; set; }
}
