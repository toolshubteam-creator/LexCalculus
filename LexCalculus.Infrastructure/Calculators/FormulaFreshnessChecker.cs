using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class FormulaFreshnessChecker : IFormulaFreshnessChecker
{
    // Frequency string values match strings used in seed/backfill.
    // Tolerance = expected period + small grace window.
    // OnLawChange / Static / Event — automatic staleness uygulanmaz.
    private static readonly Dictionary<string, int> ToleranceDays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Monthly"] = 35,
        ["Quarterly"] = 95,
        ["Biannual"] = 190,
        ["Yearly"] = 380,
    };

    public bool IsStale(FormulaParameter p, DateTime utcNow)
    {
        if (p.LastUpdatedDate is null) return false;
        if (string.IsNullOrWhiteSpace(p.ExpectedUpdateFrequency)) return false;
        if (!ToleranceDays.TryGetValue(p.ExpectedUpdateFrequency, out var days)) return false;

        return p.LastUpdatedDate.Value.AddDays(days) < utcNow;
    }

    public int? DaysUntilStale(FormulaParameter p, DateTime utcNow)
    {
        if (p.LastUpdatedDate is null) return null;
        if (string.IsNullOrWhiteSpace(p.ExpectedUpdateFrequency)) return null;
        if (!ToleranceDays.TryGetValue(p.ExpectedUpdateFrequency, out var days)) return null;

        var staleDate = p.LastUpdatedDate.Value.AddDays(days);
        return (int)(staleDate - utcNow).TotalDays;
    }
}
