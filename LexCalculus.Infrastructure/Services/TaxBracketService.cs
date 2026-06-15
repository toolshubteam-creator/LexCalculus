using System.Text.Json;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

/// <summary>
/// Dilim sorgu + marjinal dilim hesabı (Charter Karar 1). FormulaParameter
/// servisinin dilim eşdeğeri: aynı 24sa cache TTL, aynı "latest EffectiveDate
/// &lt;= asOf" lookup semantic.
///
/// Bir <c>toolSlug</c>'un farklı varyantları (örn. veraset vs ivazsız) AYRI
/// satır setleri olarak saklanır — convention: <c>tool-name/variant</c>.
/// </summary>
public sealed class TaxBracketService : ITaxBracketService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);
    private const string CacheKeyPrefix = "tax-bracket";

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TaxBracketService> _logger;

    public TaxBracketService(ApplicationDbContext db, IDistributedCache cache, ILogger<TaxBracketService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TaxBracket>> GetBracketsAsync(string toolSlug, DateTime asOf, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSlug);

        var normalized = asOf.Date;
        var cacheKey = $"{CacheKeyPrefix}:{toolSlug}:{normalized:yyyy-MM-dd}";

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<List<TaxBracket>>(cached) ?? new List<TaxBracket>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to DB.", cacheKey);
        }

        var effectiveDate = await _db.TaxBrackets
            .Where(b => b.ToolSlug == toolSlug && b.EffectiveDate <= normalized)
            .OrderByDescending(b => b.EffectiveDate)
            .Select(b => (DateTime?)b.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        if (effectiveDate is null) return Array.Empty<TaxBracket>();

        var brackets = await _db.TaxBrackets
            .Where(b => b.ToolSlug == toolSlug && b.EffectiveDate == effectiveDate)
            .OrderBy(b => b.Sira)
            .ToListAsync(ct);

        try
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(brackets),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DefaultCacheTtl }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {CacheKey}; continuing without cache.", cacheKey);
        }

        return brackets;
    }

    public async Task<DilimliVergiSonuc> HesaplaAsync(string toolSlug, decimal vergilendirilebilirTutar, DateTime asOf, CancellationToken ct = default)
    {
        var brackets = await GetBracketsAsync(toolSlug, asOf, ct);
        var detaylar = new List<DilimDetay>();
        decimal toplam = 0m;

        if (vergilendirilebilirTutar <= 0m || brackets.Count == 0)
            return new DilimliVergiSonuc { ToplamVergi = 0m, DilimDetaylar = detaylar };

        foreach (var b in brackets)
        {
            if (vergilendirilebilirTutar <= b.MinAmount) break;

            var ust = b.MaxAmount ?? decimal.MaxValue;
            var dilimUst = Math.Min(vergilendirilebilirTutar, ust);
            var dilimdekiTutar = Math.Max(0m, dilimUst - b.MinAmount);

            if (dilimdekiTutar <= 0m) continue;

            var dilimVergisi = Math.Round(dilimdekiTutar * b.Rate, 2, MidpointRounding.AwayFromZero);
            toplam += dilimVergisi;

            detaylar.Add(new DilimDetay
            {
                Sira = b.Sira,
                MinAmount = b.MinAmount,
                MaxAmount = b.MaxAmount,
                DilimdekiTutar = dilimdekiTutar,
                Rate = b.Rate,
                DilimVergisi = dilimVergisi
            });

            if (vergilendirilebilirTutar <= ust) break;
        }

        return new DilimliVergiSonuc
        {
            ToplamVergi = Math.Round(toplam, 2, MidpointRounding.AwayFromZero),
            DilimDetaylar = detaylar
        };
    }
}
