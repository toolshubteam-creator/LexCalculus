using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LexCalculus.Tests.Services;

public class CalculationHistoryServiceTests : SqlServerTestBase
{
    [Fact]
    public async Task Anonim_Kullanici_Loglanmaz()
    {
        await using var ctx = _db.Create();
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        await svc.LogAsync<object, object>(
            userId: null,
            categorySlug: "faiz",
            toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz",
            input: new { Foo = 1 },
            result: new { Bar = 2 },
            totalAmount: 100m,
            unit: "TL");

        var count = await ctx.Set<CalculationHistory>().CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Sifir_Veya_Negatif_UserId_Loglanmaz()
    {
        await using var ctx = _db.Create();
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        await svc.LogAsync<object, object>(
            userId: 0,
            categorySlug: "faiz",
            toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz",
            input: new { Foo = 1 },
            result: new { Bar = 2 },
            totalAmount: 100m,
            unit: "TL");

        var count = await ctx.Set<CalculationHistory>().CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Logged_In_Kullanici_Loglanir()
    {
        await using var ctx = _db.Create(actAsUserId: 123);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        await svc.LogAsync(
            userId: 123,
            categorySlug: "faiz",
            toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz",
            input: new { AnaPara = 100000m, TemerrutTarihi = "2024-01-01" },
            result: new { TotalAmount = 11781.91m },
            totalAmount: 11781.91m,
            unit: "TL");

        var entry = await ctx.Set<CalculationHistory>().FirstOrDefaultAsync();
        entry.Should().NotBeNull();
        entry!.UserId.Should().Be(123);
        entry.ToolSlug.Should().Be("yasal-faiz");
        entry.ToolTitle.Should().Be("Yasal Faiz");
        entry.TotalAmount.Should().Be(11781.91m);
        entry.Unit.Should().Be("TL");
        entry.InputJson.Should().Contain("anaPara");
        entry.OutputJson.Should().Contain("totalAmount");
    }

    [Fact]
    public async Task Buyuk_Payload_Atlanir()
    {
        await using var ctx = _db.Create();
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        var bigOutput = new { Data = new string('x', 600_000) };

        await svc.LogAsync(
            userId: 123,
            categorySlug: "faiz",
            toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz",
            input: new { Foo = 1 },
            result: bigOutput,
            totalAmount: 100m,
            unit: "TL");

        var count = await ctx.Set<CalculationHistory>().CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Birden_Fazla_Log_Sirayla()
    {
        await using var ctx = _db.Create(actAsUserId: 123);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        for (int i = 0; i < 5; i++)
        {
            await svc.LogAsync(
                userId: 123,
                categorySlug: "faiz",
                toolSlug: "yasal-faiz",
                toolTitle: "Yasal Faiz",
                input: new { Iteration = i },
                result: new { Result = i * 100 },
                totalAmount: i * 100m,
                unit: "TL");
        }

        var entries = await ctx.Set<CalculationHistory>()
            .OrderBy(x => x.Id)
            .ToListAsync();

        entries.Should().HaveCount(5);
        entries[0].InputJson.Should().Contain("\"iteration\":0");
        entries[4].InputJson.Should().Contain("\"iteration\":4");
    }

    [Fact]
    public async Task Soft_Delete_Filter_Calisir()
    {
        await using var ctx = _db.Create(actAsUserId: 123);
        var svc = new CalculationHistoryService(ctx, NullLogger<CalculationHistoryService>.Instance);

        await svc.LogAsync(
            userId: 123,
            categorySlug: "faiz",
            toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz",
            input: new { Foo = 1 },
            result: new { Bar = 2 },
            totalAmount: 100m,
            unit: "TL");

        var entry = await ctx.Set<CalculationHistory>().FirstAsync();
        entry.IsDeleted = true;
        await ctx.SaveChangesAsync();

        var visibleCount = await ctx.Set<CalculationHistory>().CountAsync();
        visibleCount.Should().Be(0);

        var totalCount = await ctx.Set<CalculationHistory>().IgnoreQueryFilters().CountAsync();
        totalCount.Should().Be(1);
    }
}
