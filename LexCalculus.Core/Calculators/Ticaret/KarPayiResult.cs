using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ticaret;

/// <summary>
/// H2 Kâr Payı sonucu. Yasal yedek (limit ve cap durumu), birinci ve ikinci
/// temettü, opsiyonel özel yedek ile toplam dağıtılacak temettü raporlanır.
/// </summary>
public sealed class KarPayiResult : CalculationResult
{
    public decimal YasalYedekAyrilan { get; set; }
    public decimal YasalYedekLimit { get; set; }
    public bool YasalYedekLimitDoldu { get; set; }
    public decimal BirinciTemettu { get; set; }
    public decimal OzelYedekAyrilan { get; set; }
    public decimal IkinciTemettu { get; set; }
    public decimal ToplamTemettu { get; set; }
    public decimal KarDagitilabilir { get; set; }
}
