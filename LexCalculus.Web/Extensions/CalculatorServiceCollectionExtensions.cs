using LexCalculus.Core.Calculators;
using LexCalculus.Core.Calculators.Akturya;
using LexCalculus.Core.Calculators.AileMiras;
using LexCalculus.Core.Calculators.Ceza;
using LexCalculus.Core.Calculators.Common;
using LexCalculus.Core.Calculators.Faiz;
using LexCalculus.Core.Calculators.Gayrimenkul;
using LexCalculus.Core.Calculators.IsHukuku;
using LexCalculus.Core.Calculators.Ticaret;
using LexCalculus.Core.Calculators.VergiIdare;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Infrastructure.Services;

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
        services.AddScoped<ICalculator, AracDegerKaybiCalculator>();
        services.AddScoped<ICalculator<AracDegerKaybiInput, AracDegerKaybiResult>, AracDegerKaybiCalculator>();

        // Kategori C — Faiz
        services.AddScoped<ICalculator, YasalFaizCalculator>();
        services.AddScoped<ICalculator<YasalFaizInput, YasalFaizResult>, YasalFaizCalculator>();
        services.AddScoped<ICalculator, TicariTemerrutFaizCalculator>();
        services.AddScoped<ICalculator<TicariTemerrutFaizInput, TicariTemerrutFaizResult>, TicariTemerrutFaizCalculator>();
        services.AddScoped<ICalculator, AkdiTemerrutFaizCalculator>();
        services.AddScoped<ICalculator<AkdiTemerrutFaizInput, AkdiTemerrutFaizResult>, AkdiTemerrutFaizCalculator>();
        services.AddScoped<ITUFEService, TUFEService>();
        services.AddScoped<ICalculationHistoryService, CalculationHistoryService>();
        services.AddScoped<ICalculator, KiraArtisiCalculator>();
        services.AddScoped<ICalculator<KiraArtisiInput, KiraArtisiResult>, KiraArtisiCalculator>();
        services.AddScoped<ICalculator, MenfiTespitFaizCalculator>();
        services.AddScoped<ICalculator<MenfiTespitFaizInput, MenfiTespitFaizResult>, MenfiTespitFaizCalculator>();

        // Kategori D — Gayrimenkul ve Kat Mülkiyeti (Faz 7)
        services.AddScoped<ICalculator, ArsaPayiCalculator>();
        services.AddScoped<ICalculator<ArsaPayiInput, ArsaPayiResult>, ArsaPayiCalculator>();
        services.AddScoped<ICalculator, KamulastirmaBedeliCalculator>();
        services.AddScoped<ICalculator<KamulastirmaBedeliInput, KamulastirmaBedeliResult>, KamulastirmaBedeliCalculator>();
        services.AddScoped<ICalculator, EcrimisilCalculator>();
        services.AddScoped<ICalculator<EcrimisilInput, EcrimisilResult>, EcrimisilCalculator>();
        services.AddScoped<ICalculator, KatKarsiligiInsaatCalculator>();
        services.AddScoped<ICalculator<KatKarsiligiInsaatInput, KatKarsiligiInsaatResult>, KatKarsiligiInsaatCalculator>();
        services.AddScoped<ICalculator, HasilatKiraCalculator>();
        services.AddScoped<ICalculator<HasilatKiraInput, HasilatKiraResult>, HasilatKiraCalculator>();

        // Kategori E — Aile ve Miras Hukuku (Faz 7)
        services.AddScoped<IInheritanceDistributionService, InheritanceDistributionService>();
        services.AddScoped<ICalculator, NafakaCalculator>();
        services.AddScoped<ICalculator<NafakaInput, NafakaResult>, NafakaCalculator>();
        services.AddScoped<ICalculator, MalRejimiTasfiyesiCalculator>();
        services.AddScoped<ICalculator<MalRejimiTasfiyesiInput, MalRejimiTasfiyesiResult>, MalRejimiTasfiyesiCalculator>();
        services.AddScoped<ICalculator, MirasPayiCalculator>();
        services.AddScoped<ICalculator<MirasPayiInput, MirasPayiResult>, MirasPayiCalculator>();
        services.AddScoped<ICalculator, TenkisCalculator>();
        services.AddScoped<ICalculator<TenkisInput, TenkisResult>, TenkisCalculator>();

        // Kategori F — Ceza Hukuku ve İnfaz (Faz 7)
        services.AddSingleton<ICriminalCalendarService, CriminalCalendarService>();
        services.AddScoped<ICalculator, CezaErtelemeCalculator>();
        services.AddScoped<ICalculator<CezaErtelemeInput, CezaErtelemeResult>, CezaErtelemeCalculator>();
        services.AddScoped<ICalculator, KosulluSaliverilmeCalculator>();
        services.AddScoped<ICalculator<KosulluSaliverilmeInput, KosulluSaliverilmeResult>, KosulluSaliverilmeCalculator>();
        services.AddScoped<ICalculator, DavaZamanasimiCalculator>();
        services.AddScoped<ICalculator<DavaZamanasimiInput, DavaZamanasimiResult>, DavaZamanasimiCalculator>();
        services.AddScoped<ICalculator, AdliParaCezasiCalculator>();
        services.AddScoped<ICalculator<AdliParaCezasiInput, AdliParaCezasiResult>, AdliParaCezasiCalculator>();
        services.AddScoped<ICalculator, TutuklulukMahsubuCalculator>();
        services.AddScoped<ICalculator<TutuklulukMahsubuInput, TutuklulukMahsubuResult>, TutuklulukMahsubuCalculator>();

        // Kategori G — Vergi ve İdare (Faz 7)
        services.AddScoped<ITaxBracketService, TaxBracketService>();
        services.AddScoped<ICalculator, VerasetVergisiCalculator>();
        services.AddScoped<ICalculator<VerasetVergisiInput, VerasetVergisiResult>, VerasetVergisiCalculator>();
        services.AddScoped<ICalculator, TapuHarciCalculator>();
        services.AddScoped<ICalculator<TapuHarciInput, TapuHarciResult>, TapuHarciCalculator>();
        services.AddScoped<ICalculator, DamgaVergisiCalculator>();
        services.AddScoped<ICalculator<DamgaVergisiInput, DamgaVergisiResult>, DamgaVergisiCalculator>();
        services.AddScoped<ICalculator, KdvIadesiCalculator>();
        services.AddScoped<ICalculator<KdvIadesiInput, KdvIadesiResult>, KdvIadesiCalculator>();
        services.AddScoped<ICalculator, VergiCezasiCalculator>();
        services.AddScoped<ICalculator<VergiCezasiInput, VergiCezasiResult>, VergiCezasiCalculator>();

        // Kategori H — Ticaret Hukuku (Faz 7, Dalga C)
        services.AddScoped<ICalculator, SirketTasfiyePayiCalculator>();
        services.AddScoped<ICalculator<SirketTasfiyePayiInput, SirketTasfiyePayiResult>, SirketTasfiyePayiCalculator>();
        services.AddScoped<ICalculator, KarPayiCalculator>();
        services.AddScoped<ICalculator<KarPayiInput, KarPayiResult>, KarPayiCalculator>();
        services.AddScoped<ICalculator, SozlesmeCezasiCalculator>();
        services.AddScoped<ICalculator<SozlesmeCezasiInput, SozlesmeCezasiResult>, SozlesmeCezasiCalculator>();

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
