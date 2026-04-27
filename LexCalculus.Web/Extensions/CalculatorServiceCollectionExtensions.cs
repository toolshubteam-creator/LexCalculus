using LexCalculus.Core.Calculators;
using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Infrastructure.Calculators;

namespace LexCalculus.Web.Extensions;

/// <summary>
/// Centralizes calculator registration. Each calculator is registered as a
/// Scoped ICalculator (so DI can enumerate them) and the registry consumes
/// all of them via IEnumerable&lt;ICalculator&gt;.
///
/// As real calculators arrive, replace the placeholder line with the real
/// implementation. Phase 2.5 onwards.
/// </summary>
public static class CalculatorServiceCollectionExtensions
{
    public static IServiceCollection AddCalculators(this IServiceCollection services)
    {
        // Kategori A — İş Hukuku
        services.AddScoped<ICalculator, KidemTazminatiCalculator>();
        services.AddScoped<ICalculator<KidemTazminatiInput, KidemTazminatiResult>, KidemTazminatiCalculator>();
        services.AddScoped<ICalculator, IhbarTazminatiCalculator>();
        services.AddScoped<ICalculator<IhbarTazminatiInput, IhbarTazminatiResult>, IhbarTazminatiCalculator>();
        services.AddScoped<ICalculator, YillikIzinCalculator>();
        services.AddScoped<ICalculator<YillikIzinInput, YillikIzinResult>, YillikIzinCalculator>();
        services.AddScoped<ICalculator, FazlaMesaiCalculator>();
        services.AddScoped<ICalculator<FazlaMesaiInput, FazlaMesaiResult>, FazlaMesaiCalculator>();
        services.AddScoped<ICalculator, IseIadeCalculator>();
        services.AddScoped<ICalculator<IseIadeInput, IseIadeResult>, IseIadeCalculator>();
        services.AddScoped<ICalculator, AsgariUcretCalculator>();
        services.AddScoped<ICalculator<AsgariUcretInput, AsgariUcretResult>, AsgariUcretCalculator>();
        services.AddScoped<ICalculator, MobbingCalculator>();
        services.AddScoped<ICalculator<MobbingInput, MobbingResult>, MobbingCalculator>();

        // Kategori B — Aktüerya
        services.AddScoped<ICalculator, DesteKtenYoksunKalmaCalculator>();
        services.AddScoped<ICalculator<DesteKtenYoksunKalmaInput, DesteKtenYoksunKalmaResult>, DesteKtenYoksunKalmaCalculator>();
        services.AddScoped<ICalculator, MaluliyetCalculator>();
        services.AddScoped<ICalculator<MaluliyetInput, MaluliyetResult>, MaluliyetCalculator>();
        services.AddScoped<ICalculator, GeciciIsGoremezlikCalculator>();
        services.AddScoped<ICalculator<GeciciIsGoremezlikInput, GeciciIsGoremezlikResult>, GeciciIsGoremezlikCalculator>();
        services.AddScoped<ICalculator, BakiciGideriCalculator>();
        services.AddScoped<ICalculator<BakiciGideriInput, BakiciGideriResult>, BakiciGideriCalculator>();
        services.AddScoped<ICalculator, AracDegerKaybiPlaceholder>();

        // Kategori C — Faiz
        services.AddScoped<ICalculator, YasalFaizPlaceholder>();
        services.AddScoped<ICalculator, TicariFaizPlaceholder>();
        services.AddScoped<ICalculator, TemerrutFaiziPlaceholder>();
        services.AddScoped<ICalculator, KiraArtisiPlaceholder>();
        services.AddScoped<ICalculator, MenfiTespitPlaceholder>();

        // Registry — Singleton, eagerly resolves all ICalculator instances
        services.AddSingleton<ICalculatorRegistry>(sp =>
        {
            using var scope = sp.CreateScope();
            var calculators = scope.ServiceProvider.GetServices<ICalculator>();
            return new CalculatorRegistry(calculators);
        });

        return services;
    }
}
