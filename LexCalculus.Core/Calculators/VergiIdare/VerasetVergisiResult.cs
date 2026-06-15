using LexCalculus.Core.Models.Calculators;
using LexCalculus.Core.Services;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G1 Veraset Vergisi sonucu. Brüt değer, uygulanan istisna ve dilim dökümü
/// ile birlikte toplam vergi raporlanır.
/// </summary>
public sealed class VerasetVergisiResult : CalculationResult
{
    public IntikalTuru IntikalTuru { get; set; }
    public decimal BrutDeger { get; set; }
    public decimal KisiBasinaIstisna { get; set; }
    public decimal ToplamIstisna { get; set; }
    public decimal VergilendirilebilirTutar { get; set; }
    public IReadOnlyList<DilimDetay> DilimDetaylar { get; set; } = Array.Empty<DilimDetay>();
    public decimal ToplamVergi { get; set; }
}
