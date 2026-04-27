using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Calculators;

public sealed class Three095CommercialRateService : IThree095CommercialRateService
{
    private const decimal BesPuanEsigi = 0.05m;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<Three095CommercialRateService> _logger;

    public Three095CommercialRateService(ApplicationDbContext db, ILogger<Three095CommercialRateService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<InterestRatePeriod>> GetCommercialPeriodsAsync(
        DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("Bitiş tarihi başlangıçtan önce olamaz.");

        var allRates = await _db.Set<FormulaParameter>()
            .AsNoTracking()
            .Where(p => p.ToolSlug == "tcmb-avans" && p.Key == "yillik-oran")
            .OrderBy(p => p.EffectiveDate)
            .Select(p => new { p.EffectiveDate, p.Value })
            .ToListAsync(ct);

        if (allRates.Count == 0)
        {
            _logger.LogWarning("TCMB avans oranları bulunamadı");
            return Array.Empty<InterestRatePeriod>();
        }

        decimal? GetRateAt(DateTime date)
        {
            var rate = allRates.LastOrDefault(r => r.EffectiveDate <= date);
            return rate?.Value;
        }

        // 3095 m.2 algoritması:
        //   Yıl Y'nin ilk yarısı (1 Ocak - 30 Haziran): rate = (Y-1) yılın 31 Aralık'taki TCMB avans
        //   Yıl Y'nin ikinci yarısı (1 Temmuz - 31 Aralık): rate = Y yılın 30 Haziran'daki TCMB avans
        //     EĞER (Y-1)/12/31 oranından >= 5 puan farklıysa; aksi halde ilk yarı oranı devam eder.

        var periods = new List<InterestRatePeriod>();

        var basYil = startDate.Year;
        var bitisYil = endDate.Year;

        for (var yil = basYil; yil <= bitisYil; yil++)
        {
            var ilkYariBas = new DateTime(yil, 1, 1);
            var ilkYariBitis = new DateTime(yil, 6, 30);
            var oncekiYilDec31 = new DateTime(yil - 1, 12, 31);
            var ilkYariOran = GetRateAt(oncekiYilDec31);

            if (ilkYariOran.HasValue)
            {
                var donemBas = ilkYariBas < startDate ? startDate : ilkYariBas;
                var donemBitis = ilkYariBitis > endDate ? endDate : ilkYariBitis;
                if (donemBitis >= donemBas)
                {
                    periods.Add(new InterestRatePeriod(donemBas, donemBitis, ilkYariOran.Value));
                }
            }

            var ikinciYariBas = new DateTime(yil, 7, 1);
            var ikinciYariBitis = new DateTime(yil, 12, 31);
            var bunYilJun30 = new DateTime(yil, 6, 30);
            var bunYilJun30Oran = GetRateAt(bunYilJun30);
            var oncekiDec31Oran = ilkYariOran;

            decimal? ikinciYariOran;
            if (oncekiDec31Oran.HasValue && bunYilJun30Oran.HasValue)
            {
                var fark = Math.Abs(bunYilJun30Oran.Value - oncekiDec31Oran.Value);
                ikinciYariOran = fark >= BesPuanEsigi ? bunYilJun30Oran.Value : oncekiDec31Oran.Value;
            }
            else
            {
                ikinciYariOran = bunYilJun30Oran ?? oncekiDec31Oran;
            }

            if (ikinciYariOran.HasValue)
            {
                var donemBas = ikinciYariBas < startDate ? startDate : ikinciYariBas;
                var donemBitis = ikinciYariBitis > endDate ? endDate : ikinciYariBitis;
                if (donemBitis >= donemBas)
                {
                    periods.Add(new InterestRatePeriod(donemBas, donemBitis, ikinciYariOran.Value));
                }
            }
        }

        // Ardışık aynı oranlı dönemleri birleştir
        var birlesik = new List<InterestRatePeriod>();
        foreach (var p in periods)
        {
            if (birlesik.Count > 0 && birlesik[^1].AnnualRate == p.AnnualRate
                && (p.Start - birlesik[^1].End).Days == 1)
            {
                var prev = birlesik[^1];
                birlesik[^1] = prev with { End = p.End };
            }
            else
            {
                birlesik.Add(p);
            }
        }

        return birlesik;
    }
}
