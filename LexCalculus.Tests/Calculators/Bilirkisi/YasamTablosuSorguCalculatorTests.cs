using FluentAssertions;
using LexCalculus.Core.Calculators.Bilirkisi;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators.Bilirkisi;

public class YasamTablosuSorguCalculatorTests : SqlServerTestBase
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private (YasamTablosuSorguCalculator calc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = _db.Create();
        var trh = new LifeTable
        {
            Code = "TRH-TEST",
            Name = "TRH Test",
            EffectiveDate = new DateTime(2010, 1, 1),
            IsActive = true,
            Rows = new List<LifeTableRow>
            {
                new() { Yas = 0, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 76.50m },
                new() { Yas = 0, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 82.00m },
                new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 44.45m },
                new() { Yas = 30, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 49.00m },
                new() { Yas = 31, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 43.50m },
                new() { Yas = 32, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 42.55m },
                new() { Yas = 65, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 14.04m },
                new() { Yas = 105, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 1.20m }
            }
        };
        ctx.Set<LifeTable>().Add(trh);
        ctx.SaveChanges();

        var lifeSvc = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);
        return (new YasamTablosuSorguCalculator(lifeSvc), ctx);
    }

    [Fact]
    public async Task StandardSingle_AgeGenderLookup()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new YasamTablosuSorguInput
        {
            SorguTipi = YasamSorguTipi.TekKisi,
            Cinsiyet = Cinsiyet.Erkek,
            Yas = 30
        });

        r.IsValid.Should().BeTrue();
        r.TekSonuc.Should().NotBeNull();
        r.TekSonuc!.KalanYasamUmidi.Should().Be(44.45m);
    }

    [Fact]
    public async Task AgeRange_MultipleResults_3Years()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new YasamTablosuSorguInput
        {
            SorguTipi = YasamSorguTipi.YasAraligi,
            Cinsiyet = Cinsiyet.Erkek,
            BaslangicYas = 30,
            BitisYas = 32
        });

        r.IsValid.Should().BeTrue();
        r.AralikSonuclari.Should().HaveCount(3);
        r.AralikSonuclari[0].Yas.Should().Be(30);
        r.AralikSonuclari[0].KalanYasamUmidi.Should().Be(44.45m);
        r.AralikSonuclari[2].Yas.Should().Be(32);
        r.AralikSonuclari[2].KalanYasamUmidi.Should().Be(42.55m);
    }

    [Fact]
    public async Task Boundary_Age0_And_Age105()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r0 = await calc.CalculateAsync(new YasamTablosuSorguInput
        {
            SorguTipi = YasamSorguTipi.TekKisi,
            Cinsiyet = Cinsiyet.Kadin,
            Yas = 0
        });
        r0.IsValid.Should().BeTrue();
        r0.TekSonuc!.KalanYasamUmidi.Should().Be(82.00m);

        var r105 = await calc.CalculateAsync(new YasamTablosuSorguInput
        {
            SorguTipi = YasamSorguTipi.TekKisi,
            Cinsiyet = Cinsiyet.Erkek,
            Yas = 105
        });
        r105.IsValid.Should().BeTrue();
        r105.TekSonuc!.KalanYasamUmidi.Should().Be(1.20m);
    }

    [Fact]
    public async Task ValidationError_AgeOutOfRange()
    {
        var (calc, ctx) = Build();
        await using var _ = ctx;

        var r = await calc.CalculateAsync(new YasamTablosuSorguInput
        {
            SorguTipi = YasamSorguTipi.TekKisi,
            Cinsiyet = Cinsiyet.Erkek,
            Yas = 200
        });

        r.IsValid.Should().BeFalse();
        r.ValidationErrors.Should().ContainKey(nameof(YasamTablosuSorguInput.Yas));
    }
}
