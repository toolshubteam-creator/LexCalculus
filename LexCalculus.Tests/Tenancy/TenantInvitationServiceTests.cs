using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Infrastructure.Tenancy;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Tenancy;

public class TenantInvitationServiceTests : SqlServerTestBase
{
    private const string TestSiteUrl = "https://test.lexcalculus.com";

    // IDENTITY_INSERT + UNIQUE fix (Adım 5.8 P2): explicit Id atamak yerine
    // EF'in ürettiği Id'yi kullan. UserName/Email her test içinde unique olmalı —
    // suffix bunu garanti eder.
    private static ApplicationUser MakeUser(string email, int? tenantId = null) =>
        new()
        {
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

    /// <summary>
    /// Tenant + Owner ApplicationUser arası FK döngüsünü staged save ile çözer:
    /// (a) user'ı TenantId=null ile ekle, (b) Tenant'ı OwnerUserId=user.Id ile ekle,
    /// (c) user.TenantId = tenant.Id ile bağla. Owner ve Tenant entity'leri döner.
    /// InMemory provider bu döngüyü görmezdi; SQL Server "circular dependency" verir.
    /// </summary>
    private static async Task<(ApplicationUser owner, Tenant tenant)> SeedOwnerAndTenantAsync(
        ApplicationDbContext ctx,
        string ownerEmail = "owner@x.com",
        string tenantName = "Test Tenant",
        string tenantSlug = "test-tenant")
    {
        var owner = MakeUser(ownerEmail);
        ctx.Users.Add(owner);
        await ctx.SaveChangesAsync();

        var tenant = new Tenant
        {
            Name = tenantName,
            Slug = tenantSlug,
            OwnerUserId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync();

        owner.TenantId = tenant.Id;
        await ctx.SaveChangesAsync();

        return (owner, tenant);
    }

    private (ApplicationDbContext ctx, TenantInvitationService svc, Mock<IEmailService> emailMock)
        SetupServices(ApplicationDbContext ctx)
    {
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        rendererMock.Setup(x => x.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync("<html></html>");

        var seoOptions = Options.Create(new SeoSettings { SiteUrl = TestSiteUrl });
        var svc = new TenantInvitationService(
            ctx, emailMock.Object, rendererMock.Object, new NullActivityLogService(), seoOptions,
            NullLogger<TenantInvitationService>.Instance);
        return (ctx, svc, emailMock);
    }

    [Fact]
    public async Task CreateAsync_HappyPath_InvitationCreatedWithToken()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var (_, svc, emailMock) = SetupServices(ctx);

        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);

        var inv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        inv.Status.Should().Be(TenantInvitationStatus.Pending);
        inv.Token.Should().HaveLength(32);
        inv.Email.Should().Be("guest@x.com");
        inv.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        emailMock.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonOwnerNonAdmin_Throws()
    {
        var ctx = _db.Create();
        var (_, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var rando = MakeUser("rando@x.com");
        ctx.Users.Add(rando);
        await ctx.SaveChangesAsync();
        var (_, svc, _) = SetupServices(ctx);

        var act = () => svc.CreateAsync(tenant.Id, rando.Id, "guest@x.com", requesterIsAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateAsync_AdminCanInviteAnyTenant_Succeeds()
    {
        var ctx = _db.Create();
        var (_, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var admin = MakeUser("admin@x.com");
        ctx.Users.Add(admin);
        await ctx.SaveChangesAsync();
        var (_, svc, _) = SetupServices(ctx);

        var id = await svc.CreateAsync(tenant.Id, admin.Id, "guest@x.com", requesterIsAdmin: true);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_SelfInvite_Throws()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var (_, svc, _) = SetupServices(ctx);

        var act = () => svc.CreateAsync(tenant.Id, owner.Id, owner.Email!, requesterIsAdmin: false);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Kendinizi*");
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyTenantMember_ThrowsGeneric()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);

        // İkinci tenant + üyesi (başka tenant'a üye olan member). Circular staging
        // burada da geçerli: önce other-owner user, sonra otherTenant, sonra
        // member'ı oraya bağla.
        var otherOwner = MakeUser("other-owner@x.com");
        ctx.Users.Add(otherOwner);
        await ctx.SaveChangesAsync();
        var otherTenant = new Tenant
        {
            Name = "Other", Slug = "other",
            OwnerUserId = otherOwner.Id, CreatedAt = DateTime.UtcNow, IsDeleted = false
        };
        ctx.Tenants.Add(otherTenant);
        await ctx.SaveChangesAsync();
        var member = MakeUser("member@x.com");
        ctx.Users.Add(member);
        await ctx.SaveChangesAsync();
        member.TenantId = otherTenant.Id;
        await ctx.SaveChangesAsync();

        var (_, svc, _) = SetupServices(ctx);

        var act = () => svc.CreateAsync(tenant.Id, owner.Id, "member@x.com", requesterIsAdmin: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*davet edilemiyor*");
    }

    [Fact]
    public async Task CreateAsync_DuplicatePendingInvitation_OldCancelledNewCreated()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var (_, svc, _) = SetupServices(ctx);

        var id1 = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var id2 = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);

        var first = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id1);
        first.Status.Should().Be(TenantInvitationStatus.Cancelled);
        var second = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id2);
        second.Status.Should().Be(TenantInvitationStatus.Pending);
    }

    [Fact]
    public async Task LookupByTokenAsync_ValidToken_ReturnsTenantInfo()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx, tenantName: "Hukuk A");
        var (_, svc, _) = SetupServices(ctx);
        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        var res = await svc.LookupByTokenAsync(token);

        res.IsValid.Should().BeTrue();
        res.TenantName.Should().Be("Hukuk A");
        res.Email.Should().Be("guest@x.com");
    }

    [Fact]
    public async Task LookupByTokenAsync_ExpiredToken_MarksExpired()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var (_, svc, _) = SetupServices(ctx);
        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var inv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        inv.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();

        var res = await svc.LookupByTokenAsync(inv.Token);

        res.IsValid.Should().BeFalse();
        res.InvalidReason.Should().Be("expired");
        var refreshed = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        refreshed.Status.Should().Be(TenantInvitationStatus.Expired);
    }

    [Fact]
    public async Task AcceptAsync_HappyPath_UserTenantIdSet_StatusAccepted()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var guest = MakeUser("guest@x.com");
        ctx.Users.Add(guest);
        await ctx.SaveChangesAsync();
        var (_, svc, _) = SetupServices(ctx);

        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        await svc.AcceptAsync(token, userId: guest.Id);

        var guestRefreshed = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == guest.Id);
        guestRefreshed.TenantId.Should().Be(tenant.Id);
        var refreshedInv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        refreshedInv.Status.Should().Be(TenantInvitationStatus.Accepted);
        refreshedInv.AcceptedByUserId.Should().Be(guest.Id);
    }

    [Fact]
    public async Task AcceptAsync_UserAlreadyInTenant_Throws()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var guest = MakeUser("guest@x.com"); // davet sırasında null tenant
        ctx.Users.Add(guest);
        await ctx.SaveChangesAsync();
        var (_, svc, _) = SetupServices(ctx);

        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        // Davet sonrası kullanıcı başka bir tenant'a katılır — circular staging.
        var otherOwner = MakeUser("other-owner@x.com");
        ctx.Users.Add(otherOwner);
        await ctx.SaveChangesAsync();
        var otherTenant = new Tenant
        {
            Name = "Other", Slug = "other",
            OwnerUserId = otherOwner.Id, CreatedAt = DateTime.UtcNow, IsDeleted = false
        };
        ctx.Tenants.Add(otherTenant);
        await ctx.SaveChangesAsync();

        var guestTracked = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == guest.Id);
        guestTracked.TenantId = otherTenant.Id;
        await ctx.SaveChangesAsync();

        var act = () => svc.AcceptAsync(token, userId: guest.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant*");
    }

    [Fact]
    public async Task AcceptAsync_EmailMismatch_Throws()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var diff = MakeUser("different@x.com");
        ctx.Users.Add(diff);
        await ctx.SaveChangesAsync();
        var (_, svc, _) = SetupServices(ctx);

        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        var act = () => svc.AcceptAsync(token, userId: diff.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*farklı*");
    }

    [Fact]
    public async Task CancelAsync_OwnerCanCancelOwnTenantPending()
    {
        var ctx = _db.Create();
        var (owner, tenant) = await SeedOwnerAndTenantAsync(ctx);
        var (_, svc, _) = SetupServices(ctx);
        var id = await svc.CreateAsync(tenant.Id, owner.Id, "guest@x.com", requesterIsAdmin: false);

        await svc.CancelAsync(id, requestedByUserId: owner.Id, isAdmin: false);

        var inv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        inv.Status.Should().Be(TenantInvitationStatus.Cancelled);
    }
}
