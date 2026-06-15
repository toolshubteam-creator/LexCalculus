using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.Ceza;

/// <summary>
/// F5 Tutukluluk Mahsup sonucu. Net tutukluluk gün sayısı; adli para mahsubu
/// istenmişse karşılığı TL tutarı.
/// </summary>
public sealed class TutuklulukMahsubuResult : CalculationResult
{
    public int TutuklulukGunleri { get; set; }
    public decimal? MahsupTutari { get; set; }
}
