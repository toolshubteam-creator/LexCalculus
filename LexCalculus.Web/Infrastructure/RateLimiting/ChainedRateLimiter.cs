using System.Threading.RateLimiting;

namespace LexCalculus.Web.Infrastructure.RateLimiting;

/// <summary>
/// İki+ <see cref="RateLimiter"/>'ı "VE" (AND) semantiğiyle zincirler: izin
/// yalnızca TÜM alt limiter'lar verirse alınır; ilk reddeden kısa devre yapar.
/// Faz 6.12, charter §3 Karar 7 — her policy için dakika + saat çift penceresi.
///
/// Not: Pencere limiter'larında (FixedWindow) izin zaman-temelli bir sayaçtır;
/// reddedilen istekte erken pencere(ler) yine de "harcanmış" sayılabilir
/// (lease dispose pencere sayacını geri vermez). Bu, framework'ün
/// <see cref="PartitionedRateLimiter.CreateChained{TResource}"/> davranışıyla
/// aynıdır ve kabul edilebilir — önemli olan reddetme tarafında AND semantiği.
/// </summary>
public sealed class ChainedRateLimiter : RateLimiter
{
    private readonly RateLimiter[] _limiters;

    public ChainedRateLimiter(params RateLimiter[] limiters)
    {
        if (limiters is null || limiters.Length == 0)
            throw new ArgumentException("En az bir alt limiter gerekli.", nameof(limiters));
        _limiters = limiters;
    }

    public override TimeSpan? IdleDuration
    {
        get
        {
            // Zincir, alt limiter'ların EN YENİ aktivitesine göre idle'dır
            // (biri aktifse zincir aktif → null).
            TimeSpan? min = null;
            foreach (var l in _limiters)
            {
                var d = l.IdleDuration;
                if (d is null) return null;
                if (min is null || d < min) min = d;
            }
            return min;
        }
    }

    public override RateLimiterStatistics? GetStatistics() => _limiters[0].GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var acquired = new List<RateLimitLease>(_limiters.Length);
        foreach (var limiter in _limiters)
        {
            var lease = limiter.AttemptAcquire(permitCount);
            if (!lease.IsAcquired)
            {
                foreach (var a in acquired) a.Dispose();
                return lease;   // başarısız lease (metadata: Retry-After vb. taşır)
            }
            acquired.Add(lease);
        }
        return new ChainedLease(acquired);
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount, CancellationToken cancellationToken)
    {
        var acquired = new List<RateLimitLease>(_limiters.Length);
        foreach (var limiter in _limiters)
        {
            var lease = await limiter.AcquireAsync(permitCount, cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                foreach (var a in acquired) a.Dispose();
                return lease;
            }
            acquired.Add(lease);
        }
        return new ChainedLease(acquired);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        foreach (var l in _limiters) l.Dispose();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        foreach (var l in _limiters) await l.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Tüm alt lease'ler alındığında oluşturulan birleşik lease.</summary>
    private sealed class ChainedLease : RateLimitLease
    {
        private readonly List<RateLimitLease> _leases;

        public ChainedLease(List<RateLimitLease> leases) => _leases = leases;

        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames =>
            _leases.SelectMany(l => l.MetadataNames).Distinct();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            foreach (var l in _leases)
                if (l.TryGetMetadata(metadataName, out metadata)) return true;
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;
            foreach (var l in _leases) l.Dispose();
        }
    }
}
