using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
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

public class TenantRequestServiceTests : SqlServerTestBase
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

    private const string TestSiteUrl = "https://test.lexcalculus.com";

    private (
        ApplicationDbContext ctx,
        TenantRequestService svc,
        Mock<IEmailService> emailMock,
        Mock<IEmailTemplateRenderer> rendererMock) Setup()
    {
        var ctx = _db.Create();
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
        var rendererMock = new Mock<IEmailTemplateRenderer>();
        rendererMock.Setup(x => x.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("<html></html>");

        var seoOptions = Options.Create(new SeoSettings { SiteUrl = TestSiteUrl });
        var activityLog = new NullActivityLogService();
        var tenantAdmin = new TenantAdminService(ctx, activityLog);
        var svc = new TenantRequestService(
            ctx, tenantAdmin, emailMock.Object, rendererMock.Object, activityLog,
            seoOptions, NullLogger<TenantRequestService>.Instance);
        return (ctx, svc, emailMock, rendererMock);
    }

    [Fact]
    public async Task CreateRequestAsync_HappyPath_RequestCreatedAsPending()
    {
        var (ctx, svc, _, _) = Setup();
        var user = MakeUser("u");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var id = await svc.CreateRequestAsync(user.Id,
            new CreateTenantRequestInput("Hukuk Bürosu A", null, "34/12345", "Test"));

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == id);
        r.Status.Should().Be(TenantRequestStatus.Pending);
        r.ProposedName.Should().Be("Hukuk Bürosu A");
        r.BarSicilNo.Should().Be("34/12345");
    }

    [Fact]
    public async Task CreateRequestAsync_UserAlreadyInTenant_Throws()
    {
        var (ctx, svc, _, _) = Setup();

        // Circular FK staging: önce placeholder owner ile placeholder tenant,
        // sonra hedef user'ı oraya bağla.
        var placeholderOwner = MakeUser("placeholder");
        ctx.Users.Add(placeholderOwner);
        await ctx.SaveChangesAsync();

        var placeholderTenant = new Tenant
        {
            Name = "Placeholder", Slug = "placeholder",
            CreatedAt = DateTime.UtcNow, OwnerUserId = placeholderOwner.Id
        };
        ctx.Tenants.Add(placeholderTenant);
        await ctx.SaveChangesAsync();

        var user = MakeUser("u");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        user.TenantId = placeholderTenant.Id;
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateRequestAsync(user.Id,
            new CreateTenantRequestInput("Test", null, "12345", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant*");
    }

    [Fact]
    public async Task CreateRequestAsync_UserHasActivePendingRequest_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        var user = MakeUser("u");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("First", null, "111", null));

        var act = () => svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Second", null, "222", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Bekleyen*");
    }

    [Fact]
    public async Task CancelRequestAsync_HappyPath_StatusBecomesCancelled()
    {
        var (ctx, svc, _, _) = Setup();
        var user = MakeUser("u");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Test", null, "111", null));

        await svc.CancelRequestAsync(id, user.Id);

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == id);
        r.Status.Should().Be(TenantRequestStatus.Cancelled);
        r.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelRequestAsync_NotOwnRequest_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        var u1 = MakeUser("a");
        var u2 = MakeUser("b");
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(u1.Id, new CreateTenantRequestInput("Test", null, "111", null));

        var act = () => svc.CancelRequestAsync(id, u2.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*size ait değil*");
    }

    [Fact]
    public async Task CancelRequestAsync_AlreadyProcessed_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        var user = MakeUser("u");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Test", null, "111", null));
        await svc.CancelRequestAsync(id, user.Id);

        var act = () => svc.CancelRequestAsync(id, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bekleyen*");
    }

    [Fact]
    public async Task ApproveAsync_HappyPath_TenantCreatedOwnerSet()
    {
        var (ctx, svc, emailMock, _) = Setup();
        var requester = MakeUser("u");
        var admin = MakeUser("admin");
        ctx.Users.AddRange(requester, admin);
        await ctx.SaveChangesAsync();
        var reqId = await svc.CreateRequestAsync(requester.Id,
            new CreateTenantRequestInput("Hukuk Bürosu B", null, "111", null));

        await svc.ApproveAsync(reqId, admin.Id, new ApproveTenantRequestInput("Hukuk Bürosu B", null));

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == reqId);
        r.Status.Should().Be(TenantRequestStatus.Approved);
        r.CreatedTenantId.Should().NotBeNull();

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == r.CreatedTenantId);
        tenant.OwnerUserId.Should().Be(requester.Id);

        var owner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == requester.Id);
        owner.TenantId.Should().Be(tenant.Id);

        emailMock.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_SlugCollision_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        var first = MakeUser("first");
        var second = MakeUser("second");
        var admin = MakeUser("admin");
        ctx.Users.AddRange(first, second, admin);
        await ctx.SaveChangesAsync();

        // First user pre-existing tenant with slug "shared"
        var firstReq = await svc.CreateRequestAsync(first.Id, new CreateTenantRequestInput("X", "shared", "111", null));
        await svc.ApproveAsync(firstReq, admin.Id, new ApproveTenantRequestInput("X", "shared"));

        // Second user request, admin tries same slug
        var secondReq = await svc.CreateRequestAsync(second.Id, new CreateTenantRequestInput("Y", "shared", "222", null));

        var act = () => svc.ApproveAsync(secondReq, admin.Id, new ApproveTenantRequestInput("Y", "shared"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Slug*");
    }

    [Fact]
    public async Task ApproveAsync_StatusNotPending_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        var user = MakeUser("u");
        var admin = MakeUser("a");
        ctx.Users.AddRange(user, admin);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Test", null, "111", null));
        await svc.CancelRequestAsync(id, user.Id);

        var act = () => svc.ApproveAsync(id, admin.Id, new ApproveTenantRequestInput("Test", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*işlenmiş*");
    }

    [Fact]
    public async Task RejectAsync_HappyPath_StatusBecomesRejectedWithReason()
    {
        var (ctx, svc, emailMock, _) = Setup();
        var user = MakeUser("u");
        var admin = MakeUser("a");
        ctx.Users.AddRange(user, admin);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Test", null, "111", null));

        await svc.RejectAsync(id, admin.Id, "Eksik belge");

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == id);
        r.Status.Should().Be(TenantRequestStatus.Rejected);
        r.RejectionReason.Should().Be("Eksik belge");
        r.ProcessedAt.Should().NotBeNull();
        r.ProcessedByUserId.Should().Be(admin.Id);

        emailMock.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_EmailModel_SiteUrlFromConfig()
    {
        var (ctx, svc, _, rendererMock) = Setup();
        var user = MakeUser("u");
        var admin = MakeUser("a");
        ctx.Users.AddRange(user, admin);
        await ctx.SaveChangesAsync();
        var reqId = await svc.CreateRequestAsync(user.Id,
            new CreateTenantRequestInput("Hukuk Bürosu C", null, "111", null));

        LexCalculus.Core.Email.Models.TenantRequestApprovedModel? captured = null;
        rendererMock
            .Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((_, model, _) =>
            {
                if (model is LexCalculus.Core.Email.Models.TenantRequestApprovedModel m)
                    captured = m;
            })
            .ReturnsAsync("<html></html>");

        await svc.ApproveAsync(reqId, admin.Id, new ApproveTenantRequestInput("Hukuk Bürosu C", null));

        captured.Should().NotBeNull();
        captured!.SiteUrl.Should().Be(TestSiteUrl);
    }

    [Fact]
    public async Task RejectAsync_EmailModel_SiteUrlFromConfig()
    {
        var (ctx, svc, _, rendererMock) = Setup();
        var user = MakeUser("u");
        var admin = MakeUser("a");
        ctx.Users.AddRange(user, admin);
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(user.Id, new CreateTenantRequestInput("Reject Test", null, "111", null));

        LexCalculus.Core.Email.Models.TenantRequestRejectedModel? captured = null;
        rendererMock
            .Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((_, model, _) =>
            {
                if (model is LexCalculus.Core.Email.Models.TenantRequestRejectedModel m)
                    captured = m;
            })
            .ReturnsAsync("<html></html>");

        await svc.RejectAsync(id, admin.Id, "Eksik belge");

        captured.Should().NotBeNull();
        captured!.SiteUrl.Should().Be(TestSiteUrl);
    }
}
