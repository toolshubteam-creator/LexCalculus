using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class InterestRateService : IInterestRateService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<InterestRateService> _logger;

    public InterestRateService(ApplicationDbContext db, ILogger<InterestRateService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<InterestRatePeriod>> GetRatePeriodsAsync(
        string toolSlug, string key, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("Bitiş tarihi başlangıçtan önce olamaz.");

        var allRates = await _db.Set<FormulaParameter>()
            .AsNoTracking()
            .Where(p => p.ToolSlug == toolSlug && p.Key == key && p.EffectiveDate <= endDate)
            .OrderBy(p => p.EffectiveDate)
            .ToListAsync(ct);

        if (allRates.Count == 0)
        {
            _logger.LogWarning("No rate found for {ToolSlug}/{Key}", toolSlug, key);
            return Array.Empty<InterestRatePeriod>();
        }

        var startRate = allRates.LastOrDefault(p => p.EffectiveDate <= startDate);
        if (startRate is null)
        {
            startRate = allRates.First();
            _logger.LogInformation("Start date {StartDate} predates all rates; using earliest rate from {EffectiveDate}",
                startDate, startRate.EffectiveDate);
        }

        var changesInRange = allRates
            .Where(p => p.EffectiveDate > startDate && p.EffectiveDate <= endDate)
            .OrderBy(p => p.EffectiveDate)
            .ToList();

        var periods = new List<InterestRatePeriod>();
        var currentStart = startDate;
        var currentRate = startRate.Value;

        foreach (var change in changesInRange)
        {
            var periodEnd = change.EffectiveDate.AddDays(-1);
            if (periodEnd >= currentStart)
            {
                periods.Add(new InterestRatePeriod(currentStart, periodEnd, currentRate));
            }
            currentStart = change.EffectiveDate;
            currentRate = change.Value;
        }

        if (currentStart <= endDate)
        {
            periods.Add(new InterestRatePeriod(currentStart, endDate, currentRate));
        }

        return periods;
    }

    public async Task<decimal?> GetRateAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default)
    {
        var rate = await _db.Set<FormulaParameter>()
            .AsNoTracking()
            .Where(p => p.ToolSlug == toolSlug && p.Key == key && p.EffectiveDate <= asOfDate)
            .OrderByDescending(p => p.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        return rate?.Value;
    }
}
