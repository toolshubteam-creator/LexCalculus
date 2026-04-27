using LexCalculus.Core.Entities.Common;

namespace LexCalculus.Core.Entities.Calculators;

/// <summary>
/// A time-versioned parameter used by calculator tools. Examples:
///   ToolSlug="kidem-tazminati", Key="tavan", Value=53919.68, EffectiveDate=2026-01-01
///   ToolSlug="*",                Key="asgari-ucret-brut", Value=22104.67, EffectiveDate=2026-01-01
///
/// LOOKUP SEMANTIC: Given (toolSlug, key, asOfDate), the service returns the
/// row with the LATEST EffectiveDate that is &lt;= asOfDate. This enables
/// retroactive calculations (compute as of a past date with the values that
/// were in force then).
///
/// ToolSlug "*" means a shared parameter not tied to a single tool (e.g.
/// minimum wage is used by multiple calculators).
/// </summary>
public class FormulaParameter : BaseEntity
{
    /// <summary>
    /// Tool slug this parameter belongs to. "*" for cross-tool parameters
    /// (minimum wage, legal interest rates, etc.).
    /// </summary>
    public string ToolSlug { get; set; } = string.Empty;

    /// <summary>
    /// Parameter key within the tool, e.g. "tavan", "asgari-ucret-brut",
    /// "yasal-faiz-orani". Stable across versions.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Numeric value of the parameter at this effective date. Decimal for
    /// monetary amounts and rates; calculators interpret unit per Key.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// UTC date from which this value is in force. Lookup picks the row with
    /// the greatest EffectiveDate &lt;= queryDate.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// True if this parameter is updated automatically by a background job
    /// (TCMB, Çalışma Bakanlığı API). False if entered manually by an admin.
    /// Phase 2: always false. Phase 5: Hangfire jobs flip true for relevant rows.
    /// </summary>
    public bool IsAutoUpdated { get; set; }

    /// <summary>Human-readable source label, e.g. "Çalışma Bakanlığı 2026 yıl ortası tebliği".</summary>
    public string? Source { get; set; }

    /// <summary>Free-form admin note about this version, e.g. why it changed.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// How often this parameter is expected to be updated.
    /// Used by Phase 3 admin panel for data freshness alerts.
    /// Values: "Monthly", "Yearly", "Biannual", "OnLawChange", "Static"
    /// </summary>
    public string? ExpectedUpdateFrequency { get; set; }

    /// <summary>
    /// Date this parameter was last updated from its official source
    /// (e.g., TÜİK announcement date, RG publication date).
    /// Phase 3 admin panel uses this for staleness detection.
    /// </summary>
    public DateTime? LastUpdatedDate { get; set; }

    /// <summary>
    /// Source URL, update procedure, or contextual info for the admin.
    /// Example: "TÜİK her ayın 3'ünde yayınlar — data.tuik.gov.tr"
    /// </summary>
    public string? Notes { get; set; }
}
