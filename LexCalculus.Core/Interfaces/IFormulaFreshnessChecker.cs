using LexCalculus.Core.Entities.Calculators;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Determines whether a FormulaParameter's data is stale based on
/// LastUpdatedDate + ExpectedUpdateFrequency. Single source of truth
/// used by both the admin "stale" badge (Adım 3.2) and Hangfire
/// DataFreshnessCheckJob (Adım 3.3).
/// </summary>
public interface IFormulaFreshnessChecker
{
    /// <summary>
    /// True if (LastUpdatedDate + tolerance(ExpectedUpdateFrequency)) &lt; utcNow.
    /// False if LastUpdatedDate is null (cannot determine).
    /// False for Static and OnLawChange frequencies (no automatic staleness).
    /// </summary>
    bool IsStale(FormulaParameter parameter, DateTime utcNow);

    /// <summary>
    /// Days until the parameter becomes stale. Negative if already stale.
    /// Null if frequency is Static/OnLawChange or LastUpdatedDate is null.
    /// </summary>
    int? DaysUntilStale(FormulaParameter parameter, DateTime utcNow);
}
