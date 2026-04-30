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

public class TenantInvitationServiceTests
{
    private const string TestSiteUrl = "https://test.lexcalculus.com";

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

    private static (ApplicationDbContext ctx, TenantInvitationService svc, Mock<IEmailService> emailMock)
        Setup(int? ownerUserId = null, int tenantId = 1, string tenantName = "Test Tenant", string tenantSlug = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create();
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        rendererMock.Setup(x => x.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync("<html></html>");

        if (ownerUserId.HasValue)
        {
            ctx.Tenants.Add(new Tenant
            {
                Id = tenantId, Name = tenantName, Slug = tenantSlug,
                OwnerUserId = ownerUserId.Value, CreatedAt = DateTime.UtcNow, IsDeleted = false
            });
        }

        var seoOptions = Options.Create(new SeoSettings { SiteUrl = TestSiteUrl });
        var svc = new TenantInvitationService(
            ctx, emailMock.Object, rendererMock.Object, new NullActivityLogService(), seoOptions,
            NullLogger<TenantInvitationService>.Instance);
        return (ctx, svc, emailMock);
    }

    [Fact]
    public async Task CreateAsync_HappyPath_InvitationCreatedWithToken()
    {
        var (ctx, svc, emailMock) = Setup(ownerUserId: 1);
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();

        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);

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
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(MakeUser(1, "owner@x.com", tenantId: 1), MakeUser(2, "rando@x.com"));
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateAsync(1, 2, "guest@x.com", requesterIsAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateAsync_AdminCanInviteAnyTenant_Succeeds()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(MakeUser(1, "owner@x.com", tenantId: 1), MakeUser(99, "admin@x.com"));
        await ctx.SaveChangesAsync();

        var id = await svc.CreateAsync(1, 99, "guest@x.com", requesterIsAdmin: true);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_SelfInvite_Throws()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateAsync(1, 1, "owner@x.com", requesterIsAdmin: false);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Kendinizi*");
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyTenantMember_ThrowsGeneric()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com", tenantId: 1),
            MakeUser(2, "member@x.com", tenantId: 99)); // başka tenant'ta üye
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateAsync(1, 1, "member@x.com", requesterIsAdmin: false);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*davet edilemiyor*");
    }

    [Fact]
    public async Task CreateAsync_DuplicatePendingInvitation_OldCancelledNewCreated()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();

        var id1 = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
        var id2 = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);

        var first = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id1);
        first.Status.Should().Be(TenantInvitationStatus.Cancelled);
        var second = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id2);
        second.Status.Should().Be(TenantInvitationStatus.Pending);
    }

    [Fact]
    public async Task LookupByTokenAsync_ValidToken_ReturnsTenantInfo()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1, tenantName: "Hukuk A");
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        var res = await svc.LookupByTokenAsync(token);

        res.IsValid.Should().BeTrue();
        res.TenantName.Should().Be("Hukuk A");
        res.Email.Should().Be("guest@x.com");
    }

    [Fact]
    public async Task LookupByTokenAsync_ExpiredToken_MarksExpired()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
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
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com", tenantId: 1),
            MakeUser(2, "guest@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        await svc.AcceptAsync(token, userId: 2);

        var guest = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 2);
        guest.TenantId.Should().Be(1);
        var refreshedInv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        refreshedInv.Status.Should().Be(TenantInvitationStatus.Accepted);
        refreshedInv.AcceptedByUserId.Should().Be(2);
    }

    [Fact]
    public async Task AcceptAsync_UserAlreadyInTenant_Throws()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com", tenantId: 1),
            MakeUser(2, "guest@x.com"));   // davet sırasında null tenant
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        // Davet sonrası kullanıcı başka bir tenant'a katılır
        var guest = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 2);
        guest.TenantId = 99;
        await ctx.SaveChangesAsync();

        var act = () => svc.AcceptAsync(token, userId: 2);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant*");
    }

    [Fact]
    public async Task AcceptAsync_EmailMismatch_Throws()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.AddRange(
            MakeUser(1, "owner@x.com", tenantId: 1),
            MakeUser(2, "different@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);
        var token = (await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id)).Token;

        var act = () => svc.AcceptAsync(token, userId: 2);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*farklı*");
    }

    [Fact]
    public async Task CancelAsync_OwnerCanCancelOwnTenantPending()
    {
        var (ctx, svc, _) = Setup(ownerUserId: 1);
        ctx.Users.Add(MakeUser(1, "owner@x.com", tenantId: 1));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateAsync(1, 1, "guest@x.com", requesterIsAdmin: false);

        await svc.CancelAsync(id, requestedByUserId: 1, isAdmin: false);

        var inv = await ctx.TenantInvitations.AsAdminQuery().FirstAsync(x => x.Id == id);
        inv.Status.Should().Be(TenantInvitationStatus.Cancelled);
    }
}
