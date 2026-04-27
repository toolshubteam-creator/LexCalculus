using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class TUFEService : ITUFEService
{
    private const string ToolSlug = "tufe-12-ay-ort";

    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<TUFEService> _logger;

    public TUFEService(ApplicationDbContext ctx, ILogger<TUFEService> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<(decimal? Oran, DateTime KullanilanAy, bool Bulundu)> GetKiraArtisOraniAsync(
        DateTime yenilenmeTarihi, CancellationToken cancellationToken = default)
    {
        var oncekiAy = new DateTime(yenilenmeTarihi.Year, yenilenmeTarihi.Month, 1).AddMonths(-1);
        var key = $"{oncekiAy.Year:D4}-{oncekiAy.Month:D2}";

        var param = await _ctx.Set<FormulaParameter>()
            .Where(p => p.ToolSlug == ToolSlug && p.Key == key)
            .OrderByDescending(p => p.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (param is null)
        {
            _logger.LogWarning("TÜFE data missing for {Key}", key);
            return (null, oncekiAy, false);
        }

        return (param.Value, oncekiAy, true);
    }
}
