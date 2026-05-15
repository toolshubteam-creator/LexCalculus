using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Calculators;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LexCalculus.Tests.Calculators;

public class FormulaParameterServiceCrudTests : SqlServerTestBase
{
    private static IDistributedCache CreateInMemoryCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static FormulaParameterService CreateService(LexCalculus.Infrastructure.Data.ApplicationDbContext ctx) =>
        new FormulaParameterService(ctx, CreateInMemoryCache(), new NullActivityLogService(), NullLogger<FormulaParameterService>.Instance);

    // SQL Server FK_FormulaParameters_AspNetUsers_LastModifiedByUserId zorunlu —
    // LastModifiedByUserId set edildiğinde gerçek bir user'ın Id'si lazım.
    private static async Task<ApplicationUser> SeedUserAsync(
        LexCalculus.Infrastructure.Data.ApplicationDbContext ctx, string suffix)
    {
        var user = new ApplicationUser
        {
            UserName = $"fp-{suffix}@x.com",
            NormalizedUserName = $"FP-{suffix}@X.COM",
            Email = $"fp-{suffix}@x.com",
            NormalizedEmail = $"FP-{suffix}@X.COM",
            FullName = $"FP {suffix}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRows_OrderedByToolSlugThenKeyThenEffectiveDateDesc()
    {
        await using var ctx = _db.Create();
        ctx.Set<FormulaParameter>().AddRange(
            new FormulaParameter { ToolSlug = "z-tool", Key = "rate", Value = 1m, EffectiveDate = new DateTime(2024, 1, 1) },
            new FormulaParameter { ToolSlug = "a-tool", Key = "tavan", Value = 50m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "a-tool", Key = "tavan", Value = 60m, EffectiveDate = new DateTime(2026, 1, 1) }
        );
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var all = await service.GetAllAsync();

        all.Should().HaveCount(3);
        all[0].ToolSlug.Should().Be("a-tool");
        all[0].EffectiveDate.Should().Be(new DateTime(2026, 1, 1));
        all[1].ToolSlug.Should().Be("a-tool");
        all[1].EffectiveDate.Should().Be(new DateTime(2025, 1, 1));
        all[2].ToolSlug.Should().Be("z-tool");
    }

    [Fact]
    public async Task UpdateAsync_MutatesRow_AndSetsLastModifiedByUserId()
    {
        await using var ctx = _db.Create();
        var modifier = await SeedUserAsync(ctx, "update");
        var seed = new FormulaParameter
        {
            ToolSlug = "kidem-tazminati",
            Key = "tavan",
            Value = 35058.58m,
            EffectiveDate = new DateTime(2024, 1, 1),
            Source = "wrong source"
        };
        ctx.Set<FormulaParameter>().Add(seed);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        seed.Source = "Hazine 05.01.2024 Genelgesi";
        seed.Value = 35058.58m;
        var updated = await service.UpdateAsync(seed, modifiedByUserId: modifier.Id);

        updated.Source.Should().Be("Hazine 05.01.2024 Genelgesi");
        updated.LastModifiedByUserId.Should().Be(modifier.Id);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenIdNotFound()
    {
        await using var ctx = _db.Create();
        var service = CreateService(ctx);

        var ghost = new FormulaParameter { Id = 99999, ToolSlug = "x", Key = "y" };

        var act = async () => await service.UpdateAsync(ghost, modifiedByUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SoftDeleteAsync_SetsIsDeletedTrue_AndRowExcludedFromGetAllAsync()
    {
        await using var ctx = _db.Create();
        var modifier = await SeedUserAsync(ctx, "delete");
        var seed = new FormulaParameter
        {
            ToolSlug = "kidem-tazminati",
            Key = "tavan",
            Value = 35058.58m,
            EffectiveDate = new DateTime(2024, 1, 1)
        };
        ctx.Set<FormulaParameter>().Add(seed);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);

        await service.SoftDeleteAsync(seed.Id, modifiedByUserId: modifier.Id);

        // GetAllAsync uses global query filter → soft-deleted row excluded
        var all = await service.GetAllAsync();
        all.Should().BeEmpty();

        // Bypass filter to verify row still exists with IsDeleted=true
        var withDeleted = await ctx.Set<FormulaParameter>()
            .IgnoreQueryFilters()
            .ToListAsync();
        withDeleted.Should().HaveCount(1);
        withDeleted[0].IsDeleted.Should().BeTrue();
        withDeleted[0].LastModifiedByUserId.Should().Be(modifier.Id);
    }
}
