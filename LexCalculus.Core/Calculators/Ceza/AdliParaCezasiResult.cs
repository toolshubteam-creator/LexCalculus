using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F4 Adli Para Cezası sonucu. Direkt veya çevrim hesabında etkin gün sayısı,
/// uygulanan günlük miktar ve toplam ceza raporlanır.
/// </summary>
public sealed class AdliParaCezasiResult : CalculationResult
{
    public decimal ToplamCeza { get; set; }
    public int EtkinGunSayisi { get; set; }
    public decimal UygulananGunlukMiktar { get; set; }
    public string? UyariMesaji { get; set; }
}
