using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class FormulaParameterServiceTests
{
    private static IDistributedCache CreateInMemoryCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static FormulaParameterService CreateService(LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) =>
        new FormulaParameterService(ctx, CreateInMemoryCache(), NullLogger<FormulaParameterService>.Instance);

    [Fact]
    public async Task GetValueAsync_Returns_Null_When_No_Rows_Exist()
    {
        await using var ctx = TestDbContextFactory.Create();
        var service = CreateService(ctx);

        var value = await service.GetValueAsync("kidem-tazminati", "tavan", new DateTime(2026, 5, 1));

        value.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_Returns_Latest_Effective_Date_LessThanOrEqual_AsOf()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 50000m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 53919.68m, EffectiveDate = new DateTime(2026, 1, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 57000m, EffectiveDate = new DateTime(2026, 7, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var value = await service.GetValueAsync("kidem-tazminati", "tavan", new DateTime(2026, 5, 1));
        value.Should().Be(53919.68m);

        value = await service.GetValueAsync("kidem-tazminati", "tavan", new DateTime(2026, 8, 15));
        value.Should().Be(57000m);

        value = await service.GetValueAsync("kidem-tazminati", "tavan", new DateTime(2024, 12, 31));
        value.Should().BeNull();
    }

    [Fact]
    public async Task GetValueAsync_Falls_Back_To_Shared_ToolSlug_When_Tool_Specific_Missing()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().Add(
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 22104.67m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var value = await service.GetValueAsync("kidem-tazminati", "asgari-ucret-brut", new DateTime(2026, 6, 1));
        value.Should().Be(22104.67m);
    }

    [Fact]
    public async Task GetValueAsync_Prefers_Tool_Specific_Over_Shared_When_Both_Exist()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "*",                Key = "x", Value = 100m, EffectiveDate = new DateTime(2026, 1, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "x", Value = 200m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var value = await service.GetValueAsync("kidem-tazminati", "x", new DateTime(2026, 6, 1));
        value.Should().Be(200m, "tool-specific row takes precedence over shared");
    }

    [Fact]
    public async Task GetParameterAsync_Returns_Full_Row_With_Source_And_Note()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().Add(new FormulaParameter
        {
            ToolSlug = "kidem-tazminati",
            Key = "tavan",
            Value = 53919.68m,
            EffectiveDate = new DateTime(2026, 1, 1),
            Source = "Çalışma Bakanlığı 2026 ilk yarı",
            Note = "Resmi gazete yayınına göre"
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var param = await service.GetParameterAsync("kidem-tazminati", "tavan", new DateTime(2026, 6, 1));

        param.Should().NotBeNull();
        param!.Value.Should().Be(53919.68m);
        param.Source.Should().Be("Çalışma Bakanlığı 2026 ilk yarı");
        param.Note.Should().Be("Resmi gazete yayınına göre");
    }

    [Fact]
    public async Task GetHistoryAsync_Returns_All_Versions_Descending()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 50000m,    EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 57000m,    EffectiveDate = new DateTime(2026, 7, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 53919.68m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var history = await service.GetHistoryAsync("kidem-tazminati", "tavan");

        history.Should().HaveCount(3);
        history[0].EffectiveDate.Should().Be(new DateTime(2026, 7, 1));
        history[1].EffectiveDate.Should().Be(new DateTime(2026, 1, 1));
        history[2].EffectiveDate.Should().Be(new DateTime(2025, 1, 1));
    }

    [Fact]
    public async Task AddAsync_Persists_New_Version_And_Invalidates_Cache()
    {
        await using var ctx = TestDbContextFactory.Create();

        ctx.Set<FormulaParameter>().Add(
            new FormulaParameter { ToolSlug = "x", Key = "y", Value = 100m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        var firstRead = await service.GetValueAsync("x", "y", new DateTime(2026, 6, 1));
        firstRead.Should().Be(100m);

        await service.AddAsync(new FormulaParameter
        {
            ToolSlug = "x", Key = "y", Value = 200m, EffectiveDate = new DateTime(2026, 5, 1)
        });

        var secondRead = await service.GetValueAsync("x", "y", new DateTime(2026, 6, 1));
        secondRead.Should().Be(200m, "AddAsync must invalidate cache so the new version is visible");
    }

    [Fact]
    public async Task GetValueAsync_Throws_On_Empty_ToolSlug()
    {
        await using var ctx = TestDbContextFactory.Create();
        var service = CreateService(ctx);

        var act = async () => await service.GetValueAsync("", "key", DateTime.UtcNow);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetValueAsync_Throws_On_Empty_Key()
    {
        await using var ctx = TestDbContextFactory.Create();
        var service = CreateService(ctx);

        var act = async () => await service.GetValueAsync("tool", "", DateTime.UtcNow);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
