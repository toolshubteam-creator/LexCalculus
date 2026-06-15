using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.VergiIdare;

/// <summary>
/// G3 Damga Vergisi sonucu. Hesaplanan, azami sınır cap'i ve ödenecek tutar
/// ayrı raporlanır; cap aktif olduğunda warning eklenir.
/// </summary>
public sealed class DamgaVergisiResult : CalculationResult
{
    public DamgaBelgeTuru BelgeTuru { get; set; }
    public decimal OranYuzde { get; set; }
    public decimal HesaplananVergi { get; set; }
    public decimal AzamiSinir { get; set; }
    public decimal OdenecekVergi { get; set; }
    public bool AzamiSinirUygulandi { get; set; }
}
