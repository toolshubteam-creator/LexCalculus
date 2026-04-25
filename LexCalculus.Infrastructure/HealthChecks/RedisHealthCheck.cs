using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexCalculus.Infrastructure.HealthChecks;

/// <summary>
/// Distributed cache (Redis) health check via IDistributedCache abstraction.
/// Treats cache failures as DEGRADED, not Unhealthy — because the application
/// can serve all requests without cache (slower but correct). Uses an
/// internal 2-second timeout so a dead Redis doesn't stall the health
/// probe past load-balancer probe timeouts.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private const string ProbeKey = "health:probe";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Combine caller's cancellation with our internal timeout
        using var timeoutCts = new CancellationTokenSource(ProbeTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            var payload = DateTime.UtcNow.Ticks.ToString();

            await _cache.SetStringAsync(
                ProbeKey,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                linkedCts.Token);

            var read = await _cache.GetStringAsync(ProbeKey, linkedCts.Token);

            if (read != payload)
            {
                return HealthCheckResult.Degraded("Cache roundtrip mismatch.");
            }

            return HealthCheckResult.Healthy("Distributed cache OK.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Internal timeout fired — Redis is slow or down
            return HealthCheckResult.Degraded(
                $"Distributed cache probe timed out after {ProbeTimeout.TotalSeconds}s. " +
                "Application continues to serve requests, possibly without cache acceleration.");
        }
        catch (Exception ex)
        {
            // All other failures (connection refused, auth, etc.)
            return HealthCheckResult.Degraded(
                $"Distributed cache error: {ex.GetType().Name}. {ex.Message}");
        }
    }
}
