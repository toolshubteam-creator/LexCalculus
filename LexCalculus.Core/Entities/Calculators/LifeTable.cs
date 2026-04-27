using LexCalculus.Core.Entities.Common;

namespace LexCalculus.Core.Entities.Calculators;

/// <summary>
/// A versioned mortality / life expectancy table used by actuarial
/// calculators. Multiple versions may coexist (TRH 2010, TRH 2025, CSO 1980)
/// — calculations select by Code, defaulting to the active table for the
/// calculation date.
/// </summary>
public class LifeTable : BaseEntity
{
    /// <summary>Stable identifier, e.g. "TRH-2010". Used in URLs/parameters.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "Türkiye Hayat Tablosu 2010".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Date the table was/is in force. Used to pick a default for retroactive calculations.</summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>Source / authority, e.g. "Hazine Müsteşarlığı 2010".</summary>
    public string? Source { get; set; }

    /// <summary>True if this table is currently the recommended default. Multiple may be Active=false but still usable.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Free-form admin note (deprecation reason, accuracy info, etc.).</summary>
    public string? Note { get; set; }

    public ICollection<LifeTableRow> Rows { get; set; } = new List<LifeTableRow>();
}
