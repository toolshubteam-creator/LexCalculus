using Microsoft.Extensions.Logging;

namespace LexCalculus.Jobs;

/// <summary>
/// Hangfire altyapısının ayakta olduğunu doğrulayan minimal job.
/// Adım 3.3.4'te DataFreshnessCheckJob ile DEĞİŞTİRİLECEK.
/// Şu an sadece her dakika logger'a "alive" yazar.
/// </summary>
public sealed class SmokeTestJob
{
    private readonly ILogger<SmokeTestJob> _logger;

    public SmokeTestJob(ILogger<SmokeTestJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync()
    {
        _logger.LogInformation("Hangfire smoke test job çalıştı: {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
