namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Service for retrieving interest rates that apply over a date range,
/// handling rate changes mid-period. Used by Faiz category calculators.
///
/// Returns a list of (start, end, rate) periods covering the requested range.
/// Each period's rate is the rate in effect on that period's start date.
/// </summary>
public interface IInterestRateService
{
    /// <summary>
    /// Splits [startDate, endDate] into sub-periods, each with the rate
    /// in effect during that sub-period.
    ///
    /// Example: rate is 9% from 2006-05-01, 15% from 2018-04-04, 9% from 2020-12-31.
    /// Query: 2017-01-01 to 2021-01-01 → 3 periods:
    ///   2017-01-01 → 2018-04-03 @ 9%
    ///   2018-04-04 → 2020-12-30 @ 15%
    ///   2020-12-31 → 2021-01-01 @ 9%
    /// </summary>
    Task<IReadOnlyList<InterestRatePeriod>> GetRatePeriodsAsync(
        string toolSlug, string key, DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// Returns the single rate in effect on the given date.
    /// </summary>
    Task<decimal?> GetRateAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default);
}

public sealed record InterestRatePeriod(DateTime Start, DateTime End, decimal AnnualRate)
{
    /// <summary>Number of days in this period (inclusive).</summary>
    public int Days => (End - Start).Days + 1;
}
