using LexCalculus.Core.Enums;
using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Bilirkisi;

public sealed class YasamTablosuSatiri
{
    public required int Yas { get; init; }
    public required decimal KalanYasamUmidi { get; init; }
}

/// <summary>
/// I1 Yaşam Tablosu Sorgu sonucu. Tek kişi ya da yaş aralığı modunda
/// kalan yaşam umudu (eX) yıl olarak raporlanır.
/// </summary>
public sealed class YasamTablosuSorguResult : CalculationResult
{
    public YasamSorguTipi SorguTipi { get; set; }
    public Cinsiyet Cinsiyet { get; set; }
    public YasamTablosuSatiri? TekSonuc { get; set; }
    public IReadOnlyList<YasamTablosuSatiri> AralikSonuclari { get; set; }
        = Array.Empty<YasamTablosuSatiri>();
}
