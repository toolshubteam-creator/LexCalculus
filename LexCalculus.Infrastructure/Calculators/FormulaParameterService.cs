using System.Text.Json;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class FormulaParameterService : IFormulaParameterService
{
    private const string SharedToolSlug = "*";
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(24);

    private const string IndexKeyPrefix = "index:formula-param";
    private const string ValueKeyPrefix = "formula-param";

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<FormulaParameterService> _logger;

    public FormulaParameterService(
        ApplicationDbContext db,
        IDistributedCache cache,
        ILogger<FormulaParameterService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<decimal?> GetValueAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default)
    {
        var param = await GetParameterAsync(toolSlug, key, asOfDate, ct);
        return param?.Value;
    }

    public async Task<FormulaParameter?> GetParameterAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalizedDate = asOfDate.Date;
        var cacheKey = BuildValueKey(toolSlug, key, normalizedDate);

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey, ct);
            if (cached is not null)
            {
                if (cached == "NULL")
                {
                    return null;
                }
                return JsonSerializer.Deserialize<FormulaParameter>(cached);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to DB.", cacheKey);
        }

        var param = await QueryDatabaseAsync(toolSlug, key, normalizedDate, ct);
        if (param is null && toolSlug != SharedToolSlug)
        {
            param = await QueryDatabaseAsync(SharedToolSlug, key, normalizedDate, ct);
        }

        try
        {
            var serialized = param is null ? "NULL" : JsonSerializer.Serialize(param);
            await _cache.SetStringAsync(cacheKey, serialized,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DefaultCacheTtl }, ct);

            await TrackKeyAsync(toolSlug, key, cacheKey, ct);
            if (param is not null && param.ToolSlug == SharedToolSlug && toolSlug != SharedToolSlug)
            {
                await TrackKeyAsync(SharedToolSlug, key, cacheKey, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {CacheKey}; result returned without caching.", cacheKey);
        }

        return param;
    }

    public async Task<IReadOnlyList<FormulaParameter>> GetHistoryAsync(string toolSlug, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _db.Set<FormulaParameter>()
            .Where(p => p.ToolSlug == toolSlug && p.Key == key)
            .OrderByDescending(p => p.EffectiveDate)
            .ToListAsync(ct);
    }

    public async Task<FormulaParameter> AddAsync(FormulaParameter parameter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        await _db.Set<FormulaParameter>().AddAsync(parameter, ct);
        await _db.SaveChangesAsync(ct);

        await InvalidateAsync(parameter.ToolSlug, parameter.Key, ct);

        _logger.LogInformation("Added parameter {ToolSlug}/{Key} effective {EffectiveDate}: {Value}",
            parameter.ToolSlug, parameter.Key, parameter.EffectiveDate, parameter.Value);

        return parameter;
    }

    public async Task InvalidateAsync(string toolSlug, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var indexKey = BuildIndexKey(toolSlug, key);

        try
        {
            var trackedJson = await _cache.GetStringAsync(indexKey, ct);
            if (trackedJson is not null)
            {
                var trackedKeys = JsonSerializer.Deserialize<HashSet<string>>(trackedJson) ?? new HashSet<string>();
                foreach (var k in trackedKeys)
                {
                    await _cache.RemoveAsync(k, ct);
                }
                await _cache.RemoveAsync(indexKey, ct);

                _logger.LogDebug("Invalidated {Count} cache entries for {ToolSlug}/{Key}.", trackedKeys.Count, toolSlug, key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for {ToolSlug}/{Key}.", toolSlug, key);
        }
    }

    public async Task<IReadOnlyList<FormulaParameter>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Set<FormulaParameter>()
            .OrderBy(p => p.ToolSlug)
            .ThenBy(p => p.Key)
            .ThenByDescending(p => p.EffectiveDate)
            .ToListAsync(ct);
    }

    public async Task<FormulaParameter> UpdateAsync(FormulaParameter parameter, int modifiedByUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        var existing = await _db.Set<FormulaParameter>()
            .FirstOrDefaultAsync(p => p.Id == parameter.Id, ct)
            ?? throw new InvalidOperationException($"FormulaParameter {parameter.Id} not found");

        var oldToolSlug = existing.ToolSlug;
        var oldKey = existing.Key;

        existing.ToolSlug = parameter.ToolSlug;
        existing.Key = parameter.Key;
        existing.Value = parameter.Value;
        existing.EffectiveDate = parameter.EffectiveDate;
        existing.Source = parameter.Source;
        existing.Note = parameter.Note;
        existing.ExpectedUpdateFrequency = parameter.ExpectedUpdateFrequency;
        existing.LastUpdatedDate = parameter.LastUpdatedDate;
        existing.Notes = parameter.Notes;
        existing.LastModifiedByUserId = modifiedByUserId;

        await _db.SaveChangesAsync(ct);

        await InvalidateAsync(existing.ToolSlug, existing.Key, ct);
        if (oldToolSlug != existing.ToolSlug || oldKey != existing.Key)
        {
            await InvalidateAsync(oldToolSlug, oldKey, ct);
        }

        _logger.LogInformation(
            "Updated parameter id={Id} {ToolSlug}/{Key} by user {UserId}",
            existing.Id, existing.ToolSlug, existing.Key, modifiedByUserId);

        return existing;
    }

    public async Task<bool> ExistsAsync(string toolSlug, string key, DateTime effectiveDate, int? excludeId = null, CancellationToken ct = default)
    {
        var q = _db.Set<FormulaParameter>()
            .Where(p => p.ToolSlug == toolSlug && p.Key == key && p.EffectiveDate == effectiveDate);
        if (excludeId.HasValue)
            q = q.Where(p => p.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    public async Task SoftDeleteAsync(int id, int modifiedByUserId, CancellationToken ct = default)
    {
        var existing = await _db.Set<FormulaParameter>()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException($"FormulaParameter {id} not found");

        existing.IsDeleted = true;
        existing.LastModifiedByUserId = modifiedByUserId;

        await _db.SaveChangesAsync(ct);

        await InvalidateAsync(existing.ToolSlug, existing.Key, ct);

        _logger.LogInformation(
            "Soft-deleted parameter id={Id} {ToolSlug}/{Key} by user {UserId}",
            existing.Id, existing.ToolSlug, existing.Key, modifiedByUserId);
    }

    private async Task<FormulaParameter?> QueryDatabaseAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct)
    {
        return await _db.Set<FormulaParameter>()
            .Where(p => p.ToolSlug == toolSlug
                     && p.Key == key
                     && p.EffectiveDate <= asOfDate)
            .OrderByDescending(p => p.EffectiveDate)
            .FirstOrDefaultAsync(ct);
    }

    private async Task TrackKeyAsync(string toolSlug, string key, string cacheKey, CancellationToken ct)
    {
        var indexKey = BuildIndexKey(toolSlug, key);

        var existingJson = await _cache.GetStringAsync(indexKey, ct);
        var tracked = existingJson is null
            ? new HashSet<string>()
            : JsonSerializer.Deserialize<HashSet<string>>(existingJson) ?? new HashSet<string>();

        tracked.Add(cacheKey);

        await _cache.SetStringAsync(indexKey, JsonSerializer.Serialize(tracked),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DefaultCacheTtl }, ct);
    }

    private static string BuildValueKey(string toolSlug, string key, DateTime date) =>
        $"{ValueKeyPrefix}:{toolSlug}:{key}:{date:yyyy-MM-dd}";

    private static string BuildIndexKey(string toolSlug, string key) =>
        $"{IndexKeyPrefix}:{toolSlug}:{key}";
}
