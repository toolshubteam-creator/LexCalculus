namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Specialized service for 3095 s.K. m.2 commercial default interest rate
/// computation. The statute defines a peculiar 6-month period rule:
///
/// - First half (Jan 1 - Jun 30): rate = TCMB avans rate as of previous year's Dec 31
/// - Second half (Jul 1 - Dec 31): rate = TCMB avans rate as of that year's Jun 30,
///   IF different from previous Dec 31 rate by 5 percentage points or more.
///   Otherwise the first half rate continues.
///
/// All other TCMB rate changes during the year are IGNORED for 3095 m.2 purposes.
///
/// This service reads raw TCMB avans rates from FormulaParameters
/// (toolSlug = "tcmb-avans") and produces the legally-correct 6-month periods.
/// </summary>
public interface IThree095CommercialRateService
{
    /// <summary>
    /// Returns 6-month periods covering [startDate, endDate], each with the
    /// commercial default rate that applies during that sub-period per 3095 m.2.
    /// </summary>
    Task<IReadOnlyList<InterestRatePeriod>> GetCommercialPeriodsAsync(
        DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
