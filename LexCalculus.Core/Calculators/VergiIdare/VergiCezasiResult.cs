using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>Bir gecikme dönemi (yıl-bazlı segment) detayı.</summary>
public sealed class GecikmeDonemi
{
    public required int Yil { get; init; }
    public required int AySayisi { get; init; }
    public required decimal AylikOran { get; init; }
    public required decimal DonemFaizi { get; init; }
}

/// <summary>
/// G5 Vergi Cezası sonucu. Ceza tutarı, dönem bazlı gecikme faizi dökümü ve
/// toplam ödenecek tutar raporlanır.
/// </summary>
public sealed class VergiCezasiResult : CalculationResult
{
    public decimal AsilVergi { get; set; }
    public decimal CezaTutari { get; set; }
    public IReadOnlyList<GecikmeDonemi> GecikmeFaiziDonemleri { get; set; } = Array.Empty<GecikmeDonemi>();
    public decimal ToplamGecikmeFaizi { get; set; }
    public decimal ToplamOdenecekTutar { get; set; }
    public int ToplamAySayisi { get; set; }
}
