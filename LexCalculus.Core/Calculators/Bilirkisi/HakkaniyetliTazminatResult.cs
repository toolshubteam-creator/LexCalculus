using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Bilirkisi;

/// <summary>
/// I3 Hakkaniyetli Tazminat sonucu. Her çarpan ayrı raporlanır (kullanıcı
/// kalemleri çıplak görebilsin); hesap her durumda hâkim takdir uyarısı içerir.
/// </summary>
public sealed class HakkaniyetliTazminatResult : CalculationResult
{
    public decimal BazTazminat { get; set; }
    public decimal KusurOrani { get; set; }
    public decimal EkonomikDurumKats { get; set; }
    public decimal OlayAgirligiKats { get; set; }
    public decimal YasKats { get; set; }
    public decimal HesaplananTazminat { get; set; }
    public string? UyariMesaji { get; set; }
}
