using LexCalculus.Core.Models.Calculators;

namespace LexCalculus.Core.Calculators.AileMiras;

/// <summary>Which spouse ends up the net creditor after liquidation.</summary>
public enum AlacakliEs
{
    Es1 = 1,
    Es2 = 2,
    Esit = 3
}

/// <summary>
/// Marital property liquidation result. Inherits the standard envelope and adds
/// each spouse's artık değer, the mutual katılma alacağı (½ of the OTHER's artık
/// değer), the netted claim, and which spouse is the net creditor.
/// </summary>
public sealed class MalRejimiTasfiyesiResult : CalculationResult
{
    public decimal Es1ArtikDeger { get; set; }
    public decimal Es2ArtikDeger { get; set; }

    public decimal Es1KisiselMal { get; set; }
    public decimal Es2KisiselMal { get; set; }

    /// <summary>Eş 1'in alacağı = Eş 2 artık değeri × ½.</summary>
    public decimal Es1KatilmaAlacagi { get; set; }

    /// <summary>Eş 2'nin alacağı = Eş 1 artık değeri × ½.</summary>
    public decimal Es2KatilmaAlacagi { get; set; }

    /// <summary>Es1KatilmaAlacagi - Es2KatilmaAlacagi (signed). Pozitif → Eş 1 alacaklı.</summary>
    public decimal NetAlacak { get; set; }

    public AlacakliEs AlacakliEs { get; set; }
}
