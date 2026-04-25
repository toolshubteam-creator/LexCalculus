using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexCalculus.Infrastructure.HealthChecks;

/// <summary>
/// Minimal Redis health check using IDistributedCache. Performs a roundtrip
/// set/get of a tiny payload. If Redis is unreachable and we fell back to
/// in-memory cache (Program.cs), the check still passes — by design, since
/// the application doesn't actually need Redis to function in dev.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private const string ProbeKey = "health:probe";
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = DateTime.UtcNow.Ticks.ToString();
            await _cache.SetStringAsync(
                ProbeKey,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);

            var read = await _cache.GetStringAsync(ProbeKey, cancellationToken);
            if (read != payload)
            {
                return HealthCheckResult.Degraded("Cache roundtrip mismatch.");
            }

            return HealthCheckResult.Healthy("Distributed cache OK.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Distributed cache error.", ex);
        }
    }
}
