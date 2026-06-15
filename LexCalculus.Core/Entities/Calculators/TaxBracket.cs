using LexCalculus.Core.Entities.Common;

namespace LexCalculus.Core.Entities.Calculators;

/// <summary>
/// A time-versioned tax bracket row used by progressive-rate calculators
/// (Charter Karar 1). Examples:
///   ToolSlug="veraset-vergisi/veraset",  Sira=1, MinAmount=0, MaxAmount=3_000_000, Rate=0.01
///   ToolSlug="veraset-vergisi/ivazsiz",  Sira=5, MinAmount=55_000_000, MaxAmount=null, Rate=0.30
///
/// LOOKUP SEMANTIC: Given (toolSlug, asOfDate), the service returns the latest
/// bracket set whose EffectiveDate is &lt;= asOfDate. Brackets within a set are
/// sorted by Sira ascending. Each row covers [MinAmount, MaxAmount); the last
/// row uses MaxAmount=null to mean "unbounded".
///
/// ToolSlug convention: <c>tool-name/variant</c> when a single tool has multiple
/// bracket sets (e.g. veraset vs ivazsız intikal). Pure <c>tool-name</c> is fine
/// for single-set tools.
/// </summary>
public class TaxBracket : BaseEntity
{
    /// <summary>
    /// Tool slug (and variant) this bracket belongs to. Examples:
    /// "veraset-vergisi/veraset", "veraset-vergisi/ivazsiz".
    /// </summary>
    public string ToolSlug { get; set; } = string.Empty;

    /// <summary>1-indexed bracket order within a set. Ascending = lower bound increasing.</summary>
    public int Sira { get; set; }

    /// <summary>Bracket lower bound (inclusive).</summary>
    public decimal MinAmount { get; set; }

    /// <summary>Bracket upper bound (exclusive). Null = unbounded (last bracket).</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>Marginal rate applied within this bracket (0.01 = %1).</summary>
    public decimal Rate { get; set; }

    /// <summary>UTC date from which this bracket set is in force.</summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>Human-readable source label, e.g. "RG 31.12.2025/33124 5.Mük., 57 Seri No'lu Tebliğ".</summary>
    public string? Source { get; set; }

    /// <summary>Free-form admin note.</summary>
    public string? Note { get; set; }

    /// <summary>True if this row is updated by a background job; false for manual seed/admin.</summary>
    public bool IsAutoUpdated { get; set; }
}
