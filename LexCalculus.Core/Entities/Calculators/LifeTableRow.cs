using LexCalculus.Core.Entities.Common;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Entities.Calculators;

/// <summary>
/// Single row of a life table: for a given age and gender, how many years
/// the person is expected to live (eX) and the probability of dying within
/// the next year (qX).
///
/// Composite uniqueness: (LifeTableId, Yas, Cinsiyet) — one row per combo.
/// </summary>
public class LifeTableRow : BaseEntity
{
    public int LifeTableId { get; set; }
    public LifeTable? LifeTable { get; set; }

    /// <summary>Age in completed years (0-99 typical range).</summary>
    public int Yas { get; set; }

    public Cinsiyet Cinsiyet { get; set; }

    /// <summary>
    /// Expected remaining lifetime (years) at this age. Decimal precision 6
    /// because life tables publish values like 50.234567.
    /// </summary>
    public decimal BekledigiYasam { get; set; }

    /// <summary>
    /// Probability of death within the next year (qx). Optional — some tables
    /// only publish eX, others publish full life-table columns. Used by
    /// advanced actuarial calculations (Phase 5+).
    /// </summary>
    public decimal? OlumOlasiligi { get; set; }
}
