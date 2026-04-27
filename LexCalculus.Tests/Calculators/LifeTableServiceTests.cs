using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class LifeTableServiceTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (LifeTableService svc, LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) Build()
    {
        var ctx = TestDbContextFactory.Create();
        var trh2010 = new LifeTable
        {
            Code = "TRH-2010",
            Name = "TRH 2010",
            EffectiveDate = new DateTime(2010, 1, 1),
            IsActive = true,
            Rows = new List<LifeTableRow>
            {
                new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 44.45m },
                new() { Yas = 30, Cinsiyet = Cinsiyet.Kadin, BekledigiYasam = 49.00m },
                new() { Yas = 65, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 14.04m }
            }
        };
        ctx.Set<LifeTable>().Add(trh2010);
        ctx.SaveChanges();
        var svc = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);
        return (svc, ctx);
    }

    [Fact]
    public async Task GetBekledigiYasam_30_Erkek_Returns_TRH2010_Value()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var ex = await svc.GetBekledigiYasamAsync(30, Cinsiyet.Erkek);

        ex.Should().Be(44.45m);
    }

    [Fact]
    public async Task GetBekledigiYasam_30_Kadin_Returns_Higher_Than_Erkek()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var erkek = await svc.GetBekledigiYasamAsync(30, Cinsiyet.Erkek);
        var kadin = await svc.GetBekledigiYasamAsync(30, Cinsiyet.Kadin);

        kadin.Should().BeGreaterThan(erkek!.Value, "kadın yaşam süresi erkekten yüksek (TRH 2010)");
    }

    [Fact]
    public async Task GetBekledigiYasam_Tablo_Yoksa_Null_Doner()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var ex = await svc.GetBekledigiYasamAsync(99, Cinsiyet.Erkek);

        ex.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTable_Returns_Active_Table()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var table = await svc.GetActiveTableAsync();

        table.Should().NotBeNull();
        table!.Code.Should().Be("TRH-2010");
    }

    [Fact]
    public async Task Birden_Fazla_Tablo_Aynı_Anda_Bulunabilir()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Set<LifeTable>().AddRange(
            new LifeTable
            {
                Code = "TRH-2010",
                Name = "TRH 2010",
                EffectiveDate = new DateTime(2010, 1, 1),
                IsActive = false,
                Rows = new List<LifeTableRow> { new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 44.45m } }
            },
            new LifeTable
            {
                Code = "TRH-2025",
                Name = "TRH 2025",
                EffectiveDate = new DateTime(2025, 1, 1),
                IsActive = true,
                Rows = new List<LifeTableRow> { new() { Yas = 30, Cinsiyet = Cinsiyet.Erkek, BekledigiYasam = 46.50m } }
            }
        );
        await ctx.SaveChangesAsync();

        var svc = new LifeTableService(ctx, CreateCache(), NullLogger<LifeTableService>.Instance);

        var defaultEx = await svc.GetBekledigiYasamAsync(30, Cinsiyet.Erkek);
        defaultEx.Should().Be(46.50m);

        var trh2010Ex = await svc.GetBekledigiYasamAsync(30, Cinsiyet.Erkek, "TRH-2010");
        trh2010Ex.Should().Be(44.45m);

        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task Negatif_Yas_Throws()
    {
        var (svc, ctx) = Build();
        await using var _ = ctx;

        var act = async () => await svc.GetBekledigiYasamAsync(-1, Cinsiyet.Erkek);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
