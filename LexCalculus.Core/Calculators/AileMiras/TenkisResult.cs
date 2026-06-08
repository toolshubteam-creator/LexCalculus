using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.AileMiras;

public sealed class SakliPaySatiri
{
    public required string Tanim { get; init; }
    public required string MirasciTuru { get; init; }
    public required decimal YasalPayKesri { get; init; }
    public required decimal SakliPayOrani { get; init; }
    public required decimal SakliPayTutari { get; init; }
}

public sealed class TenkisKalemi
{
    /// <summary>"vasiyet" veya "bagis".</summary>
    public required string Tur { get; init; }
    public required string Tanim { get; init; }
    public required decimal OrijinalTutar { get; init; }
    public required decimal TenkisTutari { get; init; }
    public decimal KalanTutar => OrijinalTutar - TenkisTutari;
}

/// <summary>
/// Tenkis result. Inherits the standard envelope and adds the saklı pay table,
/// the disposable portion (tasarruf nisabı), the violation amount, and the
/// per-disposition abatement amounts in TMK m.561 order.
/// </summary>
public sealed class TenkisResult : CalculationResult
{
    public List<SakliPaySatiri> SakliPaylar { get; set; } = new();
    public decimal TenkiseEsasMatrah { get; set; }
    public decimal ToplamSakliPay { get; set; }
    public decimal TasarrufNisabi { get; set; }
    public decimal ToplamTasarruf { get; set; }
    public bool SakliPayIhlali { get; set; }
    public decimal IhlalTutari { get; set; }
    public List<TenkisKalemi> TenkisKalemleri { get; set; } = new();
}
