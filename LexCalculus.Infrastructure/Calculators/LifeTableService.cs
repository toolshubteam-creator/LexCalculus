using System.Text.Json;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class LifeTableService : ILifeTableService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private const string ActiveCacheKey = "life-table:active";

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<LifeTableService> _logger;

    public LifeTableService(ApplicationDbContext db, IDistributedCache cache, ILogger<LifeTableService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal?> GetBekledigiYasamAsync(int yas, Cinsiyet cinsiyet, string? tableCode = null, CancellationToken ct = default)
    {
        if (yas < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(yas), "Yaş negatif olamaz.");
        }

        var cacheKey = $"life-table:ex:{tableCode ?? "default"}:{yas}:{cinsiyet}";

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                if (cached == "NULL") return null;
                return decimal.Parse(cached, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {Key}", cacheKey);
        }

        IQueryable<LifeTableRow> q = _db.Set<LifeTableRow>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(tableCode))
        {
            q = q.Where(r => r.LifeTable!.Code == tableCode);
        }
        else
        {
            q = q.Where(r => r.LifeTable!.IsActive);
        }

        var row = await q
            .Where(r => r.Yas == yas && r.Cinsiyet == cinsiyet)
            .OrderByDescending(r => r.LifeTable!.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        var value = row?.BekledigiYasam;

        try
        {
            var serialized = value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";
            await _cache.SetStringAsync(cacheKey, serialized,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {Key}", cacheKey);
        }

        return value;
    }

    public async Task<LifeTable?> GetActiveTableAsync(CancellationToken ct = default)
    {
        try
        {
            var cached = await _cache.GetStringAsync(ActiveCacheKey, ct);
            if (cached is not null)
            {
                return cached == "NULL" ? null : JsonSerializer.Deserialize<LifeTable>(cached);
            }
        }
        catch { /* ignored */ }

        var table = await _db.Set<LifeTable>()
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        try
        {
            var serialized = table is null ? "NULL" : JsonSerializer.Serialize(table);
            await _cache.SetStringAsync(ActiveCacheKey, serialized,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        }
        catch { /* ignored */ }

        return table;
    }

    public async Task<IReadOnlyList<LifeTable>> GetAllTablesAsync(CancellationToken ct = default)
    {
        return await _db.Set<LifeTable>()
            .AsNoTracking()
            .OrderByDescending(t => t.EffectiveDate)
            .ToListAsync(ct);
    }

    public async Task InvalidateCacheAsync(CancellationToken ct = default)
    {
        // 1. Aktif tablo cache'i (GetActiveTableAsync)
        await _cache.RemoveAsync(ActiveCacheKey, ct);

        // 2. Default (active table) ex cache'leri — 100 yaş × 2 cinsiyet = 200 key.
        //    IDistributedCache wildcard remove desteklemiyor; tüm olası
        //    default key'leri tek tek siliyoruz. Aktivasyon nadir bir işlem
        //    olduğu için 200 RemoveAsync çağrısı kabul edilebilir.
        var removeTasks = new List<Task>();
        foreach (var cinsiyet in Enum.GetValues<Cinsiyet>())
        {
            for (int yas = 0; yas <= 99; yas++)
            {
                removeTasks.Add(_cache.RemoveAsync($"life-table:ex:default:{yas}:{cinsiyet}", ct));
            }
        }
        await Task.WhenAll(removeTasks);

        _logger.LogInformation(
            "LifeTable cache invalidate: '{ActiveKey}' + {ExCount} ex key (default) silindi.",
            ActiveCacheKey, removeTasks.Count);
    }
}
