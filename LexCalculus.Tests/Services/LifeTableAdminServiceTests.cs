using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Interfaces;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Services;

public class LifeTableAdminServiceTests
{
    private static LifeTableAdminService CreateService(LexCalculus.Infrastructure.Data.ApplicationDbContext ctx)
    {
        var calculatorSvc = new Mock<ILifeTableService>().Object;
        return new LifeTableAdminService(
            ctx, calculatorSvc, NullLogger<LifeTableAdminService>.Instance);
    }

    private static LifeTable MakeTable(string code, DateTime effective, bool isActive) =>
        new()
        {
            Code = code,
            Name = $"Test {code}",
            EffectiveDate = effective,
            IsActive = isActive
        };

    [Fact]
    public async Task GetAllAsync_OrdersActiveFirstThenEffectiveDateDesc()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Set<LifeTable>().AddRange(
            MakeTable("OLD-2010", new DateTime(2010, 1, 1), isActive: true),
            MakeTable("NEW-2020", new DateTime(2020, 1, 1), isActive: false)
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var list = await svc.GetAllAsync();

        list.Should().HaveCount(2);
        list[0].Code.Should().Be("OLD-2010");   // active first
        list[1].Code.Should().Be("NEW-2020");
    }

    [Fact]
    public async Task ActivateAsync_DeactivatesPreviousAndActivatesTarget()
    {
        await using var ctx = TestDbContextFactory.Create();
        var t1 = MakeTable("T1", new DateTime(2010, 1, 1), isActive: true);
        var t2 = MakeTable("T2", new DateTime(2020, 1, 1), isActive: false);
        ctx.Set<LifeTable>().AddRange(t1, t2);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ActivateAsync(t2.Id);

        var refreshed = await ctx.Set<LifeTable>().AsNoTracking().ToListAsync();
        refreshed.Single(x => x.Id == t1.Id).IsActive.Should().BeFalse();
        refreshed.Single(x => x.Id == t2.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAsync_AlreadyActive_NoOp()
    {
        await using var ctx = TestDbContextFactory.Create();
        var t1 = MakeTable("T1", new DateTime(2020, 1, 1), isActive: true);
        ctx.Set<LifeTable>().Add(t1);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ActivateAsync(t1.Id);

        var refreshed = await ctx.Set<LifeTable>().AsNoTracking().FirstAsync(x => x.Id == t1.Id);
        refreshed.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateActiveAsync_TurnsOffActiveTable()
    {
        await using var ctx = TestDbContextFactory.Create();
        var t1 = MakeTable("T1", new DateTime(2020, 1, 1), isActive: true);
        ctx.Set<LifeTable>().Add(t1);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.DeactivateActiveAsync();

        var anyActive = await ctx.Set<LifeTable>().AsNoTracking().AnyAsync(x => x.IsActive);
        anyActive.Should().BeFalse();
    }
}
