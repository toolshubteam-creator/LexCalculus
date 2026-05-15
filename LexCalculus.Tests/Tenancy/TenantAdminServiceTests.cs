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

public class TenantAdminServiceTests : SqlServerTestBase
{
    // IDENTITY_INSERT fix (Adım 5.8 P2): explicit Id atamak yerine EF'in
    // ürettiği Id'yi kullan. UserName/Email her test içinde unique olmalı —
    // suffix bunu garanti eder.
    private static ApplicationUser MakeUser(string suffix, int? tenantId = null) =>
        new()
        {
            UserName = $"u{suffix}@x.com",
            NormalizedUserName = $"U{suffix}@X.COM",
            Email = $"u{suffix}@x.com",
            NormalizedEmail = $"U{suffix}@X.COM",
            FullName = $"User {suffix}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenantId,
            SecurityStamp = Guid.NewGuid().ToString()
        };

    private (ApplicationDbContext ctx, TenantAdminService svc) Setup()
    {
        var ctx = _db.Create();
        var svc = new TenantAdminService(ctx, new NullActivityLogService());
        return (ctx, svc);
    }

    [Fact]
    public async Task CreateAsync_WithAutoSlug_GeneratesFromName()
    {
        var (ctx, svc) = Setup();
        var owner = MakeUser("owner");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var id = await svc.CreateAsync(new CreateTenantRequest("Hukuk Bürosu A", null, owner.Id));

        var tenant = await ctx.Tenants.AsNoTracking().FirstAsync(t => t.Id == id);
        tenant.Slug.Should().Be("hukuk-burosu-a");
        tenant.Name.Should().Be("Hukuk Bürosu A");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSlug_Throws()
    {
        var (ctx, svc) = Setup();
        var u1 = MakeUser("a");
        var u2 = MakeUser("b");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        await svc.CreateAsync(new CreateTenantRequest("Test", "shared-slug", u1.Id));

        var act = () => svc.CreateAsync(new CreateTenantRequest("Test 2", "shared-slug", u2.Id));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Slug*");
    }

    [Fact]
    public async Task CreateAsync_WithUserAlreadyInTenant_Throws()
    {
        var (ctx, svc) = Setup();
        // Circular FK staging: önce başka bir tenant'ın owner'ı olan user'ı
        // seed et (TenantId null), sonra placeholder tenant, sonra TenantId set.
        var placeholderOwner = MakeUser("placeholder-owner");
        ctx.Users.Add(placeholderOwner);
        await ctx.SaveChangesAsync();

        var placeholderTenant = new Tenant
        {
            Name = "Placeholder", Slug = "placeholder",
            CreatedAt = DateTime.UtcNow, OwnerUserId = placeholderOwner.Id
        };
        ctx.Tenants.Add(placeholderTenant);
        await ctx.SaveChangesAsync();

        var bound = MakeUser("bound-owner");
        ctx.Users.Add(bound);
        await ctx.SaveChangesAsync();
        bound.TenantId = placeholderTenant.Id;
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateAsync(new CreateTenantRequest("Test", null, bound.Id));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task CreateAsync_HappyPath_OwnerTenantIdSet()
    {
        var (ctx, svc) = Setup();
        var owner = MakeUser("owner");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", null, owner.Id));

        var owner2 = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == owner.Id);
        owner2.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task UpdateAsync_ChangeOwner_NewOwnerTenantIdSet()
    {
        var (ctx, svc) = Setup();
        var oldOwner = MakeUser("old");
        var newOwner = MakeUser("new");
        ctx.Users.AddRange(oldOwner, newOwner);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", oldOwner.Id));
        await svc.UpdateAsync(tenantId, new UpdateTenantRequest("Test", "test", OwnerUserId: newOwner.Id));

        var refreshedNewOwner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == newOwner.Id);
        refreshedNewOwner.TenantId.Should().Be(tenantId);

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == tenantId);
        tenant.OwnerUserId.Should().Be(newOwner.Id);
    }

    [Fact]
    public async Task AddMemberAsync_UserAlreadyInTenant_Throws()
    {
        var (ctx, svc) = Setup();
        var owner = MakeUser("owner");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        // Placeholder tenant for "outsider already bound to another tenant"
        var placeholderOwner = MakeUser("placeholder-owner");
        ctx.Users.Add(placeholderOwner);
        await ctx.SaveChangesAsync();
        var placeholderTenant = new Tenant
        {
            Name = "Placeholder", Slug = "placeholder",
            CreatedAt = DateTime.UtcNow, OwnerUserId = placeholderOwner.Id
        };
        ctx.Tenants.Add(placeholderTenant);
        await ctx.SaveChangesAsync();

        var outsider = MakeUser("outsider");
        ctx.Users.Add(outsider);
        await ctx.SaveChangesAsync();
        outsider.TenantId = placeholderTenant.Id;
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", owner.Id));

        var act = () => svc.AddMemberAsync(tenantId, outsider.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task RemoveMemberAsync_RemovingOwner_Throws()
    {
        var (ctx, svc) = Setup();
        var owner = MakeUser("owner");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", owner.Id));

        var act = () => svc.RemoveMemberAsync(tenantId, owner.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Owner*");
    }

    [Fact]
    public async Task SoftDeleteAsync_SetsMembersTenantIdToNull()
    {
        var (ctx, svc) = Setup();
        var owner = MakeUser("owner");
        var member = MakeUser("member");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", owner.Id));
        await svc.AddMemberAsync(tenantId, member.Id);

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
        var owner = MakeUser("owner");
        var member = MakeUser("member");
        ctx.Users.AddRange(owner, member);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", owner.Id));
        await svc.AddMemberAsync(tenantId, member.Id);

        // Üye, paylaşılmış bir hesap kayıt eder.
        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = member.Id,
            CategorySlug = "is-hukuku",
            ToolSlug = "kidem-tazminati",
            ToolTitle = "Kıdem Tazminatı",
            InputJson = "{}",
            OutputJson = "{}",
            TenantId = tenantId
        });
        await ctx.SaveChangesAsync();

        await svc.RemoveMemberAsync(tenantId, member.Id);

        var refreshedMember = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == member.Id);
        refreshedMember.TenantId.Should().BeNull();

        // Karar 6: hesabın TenantId'si HÂLÂ aynı; paylaşım korunur.
        var calc = await ctx.Set<CalculationHistory>()
            .AsAdminQuery()
            .FirstAsync(h => h.UserId == member.Id);
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
        var owner = MakeUser("owner");
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var tenantId = await svc.CreateAsync(new CreateTenantRequest("Test", "test", owner.Id));

        ctx.Set<CalculationHistory>().Add(new CalculationHistory
        {
            UserId = owner.Id,
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

        var refreshedOwner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == owner.Id);
        refreshedOwner.TenantId.Should().BeNull();

        var calc = await ctx.Set<CalculationHistory>()
            .AsAdminQuery()
            .FirstAsync(h => h.UserId == owner.Id);
        calc.TenantId.Should().Be(tenantId);
    }
}
