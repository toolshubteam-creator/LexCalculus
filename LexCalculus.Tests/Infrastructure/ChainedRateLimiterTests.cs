using System.Threading.RateLimiting;
using FluentAssertions;
using LexCalculus.Web.Infrastructure.RateLimiting;
using Xunit;

namespace LexCalculus.Tests.Infrastructure;

/// <summary>
/// Faz 6.12 (charter §3 Karar 7) — ChainedRateLimiter AND semantiği birim
/// testleri. AutoReplenishment=false → pencere test içinde yenilenmez,
/// deterministik.
/// </summary>
public sealed class ChainedRateLimiterTests
{
    private static FixedWindowRateLimiter FixedWindow(int permitLimit) =>
        new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromHours(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = false
        });

    [Fact]
    public void AttemptAcquire_BothWindowsAllow_LeaseAcquired()
    {
        using var limiter = new ChainedRateLimiter(FixedWindow(3), FixedWindow(5));

        using var lease = limiter.AttemptAcquire(1);

        lease.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AttemptAcquire_FirstWindowExhausted_Rejected()
    {
        using var limiter = new ChainedRateLimiter(FixedWindow(2), FixedWindow(10));

        limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue();
        limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue();
        limiter.AttemptAcquire(1).IsAcquired
            .Should().BeFalse("ilk pencere (2/dk) doldu → AND reddi");
    }

    [Fact]
    public void AttemptAcquire_SecondWindowExhausted_RejectedEvenIfFirstHasRoom()
    {
        // Dakika penceresi bol (10), saat penceresi dar (2) → saat bağlayıcı.
        using var limiter = new ChainedRateLimiter(FixedWindow(10), FixedWindow(2));

        limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue();
        limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue();
        limiter.AttemptAcquire(1).IsAcquired
            .Should().BeFalse("ilk pencerede yer var ama ikinci pencere (2) doldu");
    }

    [Fact]
    public async Task AcquireAsync_BothAllow_ThenExhausted_Rejected()
    {
        await using var limiter = new ChainedRateLimiter(FixedWindow(1), FixedWindow(5));

        (await limiter.AcquireAsync(1)).IsAcquired.Should().BeTrue();
        (await limiter.AcquireAsync(1)).IsAcquired
            .Should().BeFalse("dakika penceresi (1) doldu");
    }

    [Fact]
    public void Constructor_NoLimiters_Throws()
    {
        var act = () => new ChainedRateLimiter();
        act.Should().Throw<ArgumentException>();
    }
}
