using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Tenancy;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class TenantAdminServiceTests
{
    private static ApplicationUser MakeUser(int id, string email, int? tenantId = null) =>
        new()
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenantId,
            SecurityStamp = Guid.NewGuid().ToString()
        };

    private static (ApplicationDbContext ctx, TenantAdminService svc) Setup()
    {
        var ctx = TestDbContextFactory.Create();
        var svc = new TenantAdminService(ctx);
        return (ctx, svc);
    }

    [Fact]
    public async Task CreateAsync_WithAutoSlug_GeneratesFromName()
    {
        var (ctx, svc) = Setup();
        ctx.Users.Add(MakeUser(1, "owner@x.com"));
        await ctx.SaveChangesAsync();

        var id = await svc.CreateAsync(new CreateTenantRequest("Hukuk Bürosu A", null, 1));

        var tenant = await ctx.Tenants.AsNoTracking().FirstAsync(t => t.Id == id);
        tenant.Slug.Should().Be("hukuk-burosu-a");
        tenant.Name.Should().Be("Hukuk Bürosu A");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSlug_Throws()
    {
        var (ctx, svc) = Setup();
        ctx.Users.AddRange(MakeUser(1, "a@x.com"), MakeUser(2, "b@x.com"));
        await ctx.SaveChangesAsync();

        await svc.CreateAsync(new CreateTenantRequest("Test", "shared-slug", 1));

        var act = () => svc.CreateAsync(new CreateTenantRequest("Test 2", "shared-slug", 2));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Slug*");
    }

    [Fact]
    public async Task CreateAsync_WithUserAlreadyInTenant_Throws()
    {
        var (ctx, svc) = Setup();
        ctx.Users.Add(MakeUser(1, "boundOwner@x.com", tenantId: 99));
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateAsync(new CreateTenantRequest("Test", null, 1));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task CreateAsync_HappyPath_OwnerTenantIdSet()
    {
        var (ctx, svc) = Setup();
        ctx.Users.Add(MakeUser(1, "owner@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", null, 1));

        var owner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 1);
        owner.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task UpdateAsync_ChangeOwner_NewOwnerTenantIdSet()
    {
        var (ctx, svc) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "old@x.com"),
            MakeUser(2, "new@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));
        await svc.UpdateAsync(tenantId, new UpdateTenantRequest("Test", "test", OwnerUserId: 2));

        var newOwner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 2);
        newOwner.TenantId.Should().Be(tenantId);

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == tenantId);
        tenant.OwnerUserId.Should().Be(2);
    }

    [Fact]
    public async Task AddMemberAsync_UserAlreadyInTenant_Throws()
    {
        var (ctx, svc) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "outsider@x.com", tenantId: 99));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));

        var act = () => svc.AddMemberAsync(tenantId, 2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task RemoveMemberAsync_RemovingOwner_Throws()
    {
        var (ctx, svc) = Setup();
        ctx.Users.Add(MakeUser(1, "owner@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));

        var act = () => svc.RemoveMemberAsync(tenantId, 1);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Owner*");
    }

    [Fact]
    public async Task SoftDeleteAsync_SetsMembersTenantIdToNull()
    {
        var (ctx, svc) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "member@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));
        await svc.AddMemberAsync(tenantId, 2);

        await svc.SoftDeleteAsync(tenantId);

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == tenantId);
        tenant.IsDeleted.Should().BeTrue();
        tenant.DeletedAt.Should().NotBeNull();

        var users = await ctx.Users.AsAdminQuery().ToListAsync();
        users.Should().OnlyContain(u => u.TenantId == null);
    }

    /// <summary>
    /// Karar 6 regresyonu: üye tenant'tan çıkarıldığında, o üyenin daha önce
    /// paylaştığı (TenantId set) hesapların TenantId'si KORUNMALI; ekibin
    /// diğer üyeleri o hesapları görmeye devam etmeli.
    /// </summary>
    [Fact]
    public async Task RemoveMemberAsync_PreservesCalculationHistoryTenantId()
    {
        var (ctx, svc) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com"),
            MakeUser(2, "member@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));
        await svc.AddMemberAsync(tenantId, 2);

        // Üye, paylaşılmış bir hesap kayıt eder.
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 2,
            CategorySlug = "is-hukuku",
            ToolSlug = "kidem-tazminati",
            ToolTitle = "Kıdem Tazminatı",
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = tenantId
        });
        await ctx.SaveChangesAsync();

        await svc.RemoveMemberAsync(tenantId, 2);

        var member = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 2);
        member.TenantId.Should().BeNull();

        // Karar 6: hesabın TenantId'si HÂLÂ aynı; paylaşım korunur.
        var calc = await ctx.Set<CalculationHistory>()
            .AsAdminQuery()
            .FirstAsync(h => h.UserId == 2);
        calc.TenantId.Should().Be(tenantId);
    }

    /// <summary>
    /// Karar 6 regresyonu (devam): tenant soft-delete edildiğinde, paylaşılmış
    /// hesaplara dokunulmamalı. TenantId hâlâ set kalır (filter ile gizli olsa da
    /// veri kaybedilmemeli — geri yükleme veya hard-delete cascade için).
    /// </summary>
    [Fact]
    public async Task SoftDeleteAsync_PreservesCalculationHistoryTenantId()
    {
        var (ctx, svc) = Setup();
        ctx.Users.Add(MakeUser(1, "owner@x.com"));
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", 1));

        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = 1,
            CategorySlug = "faiz",
            ToolSlug = "yasal-faiz",
            ToolTitle = "Yasal Faiz",
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = tenantId
        });
        await ctx.SaveChangesAsync();

        await svc.SoftDeleteAsync(tenantId);

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == tenantId);
        tenant.IsDeleted.Should().BeTrue();

        var owner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 1);
        owner.TenantId.Should().BeNull();

        var calc = await ctx.Set<CalculationHistory>()
            .AsAdminQuery()
            .FirstAsync(h => h.UserId == 1);
        calc.TenantId.Should().Be(tenantId);
    }
}
