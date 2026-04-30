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

public class TenantRequestServiceTests
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

    private const string TestSiteUrl = "https://test.lexcalculus.com";

    private static (
        ApplicationDbContext ctx,
        TenantRequestService svc,
        Mock<IEmailService> emailMock,
        Mock<IEmailTemplateRenderer> rendererMock) Setup()
    {
        var ctx = TestDbContextFactory.Create();
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
        ctx.Users.Add(MakeUser(1, "u@x.com"));
        await ctx.SaveChangesAsync();

        var id = await svc.CreateRequestAsync(1,
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
        ctx.Users.Add(MakeUser(1, "u@x.com", tenantId: 99));
        await ctx.SaveChangesAsync();

        var act = () => svc.CreateRequestAsync(1,
            new CreateTenantRequestInput("Test", null, "12345", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tenant*");
    }

    [Fact]
    public async Task CreateRequestAsync_UserHasActivePendingRequest_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.Add(MakeUser(1, "u@x.com"));
        await ctx.SaveChangesAsync();
        await svc.CreateRequestAsync(1, new CreateTenantRequestInput("First", null, "111", null));

        var act = () => svc.CreateRequestAsync(1, new CreateTenantRequestInput("Second", null, "222", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Bekleyen*");
    }

    [Fact]
    public async Task CancelRequestAsync_HappyPath_StatusBecomesCancelled()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.Add(MakeUser(1, "u@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Test", null, "111", null));

        await svc.CancelRequestAsync(id, 1);

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == id);
        r.Status.Should().Be(TenantRequestStatus.Cancelled);
        r.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelRequestAsync_NotOwnRequest_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.AddRange(MakeUser(1, "a@x.com"), MakeUser(2, "b@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Test", null, "111", null));

        var act = () => svc.CancelRequestAsync(id, 2);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*size ait değil*");
    }

    [Fact]
    public async Task CancelRequestAsync_AlreadyProcessed_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.Add(MakeUser(1, "u@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Test", null, "111", null));
        await svc.CancelRequestAsync(id, 1);

        var act = () => svc.CancelRequestAsync(id, 1);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bekleyen*");
    }

    [Fact]
    public async Task ApproveAsync_HappyPath_TenantCreatedOwnerSet()
    {
        var (ctx, svc, emailMock, _) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "u@x.com"),
            MakeUser(99, "admin@x.com"));
        await ctx.SaveChangesAsync();
        var reqId = await svc.CreateRequestAsync(1,
            new CreateTenantRequestInput("Hukuk Bürosu B", null, "111", null));

        await svc.ApproveAsync(reqId, 99, new ApproveTenantRequestInput("Hukuk Bürosu B", null));

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == reqId);
        r.Status.Should().Be(TenantRequestStatus.Approved);
        r.CreatedTenantId.Should().NotBeNull();

        var tenant = await ctx.Tenants.AsAdminQuery().FirstAsync(t => t.Id == r.CreatedTenantId);
        tenant.OwnerUserId.Should().Be(1);

        var owner = await ctx.Users.AsAdminQuery().FirstAsync(u => u.Id == 1);
        owner.TenantId.Should().Be(tenant.Id);

        emailMock.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_SlugCollision_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.AddRange(
            MakeUser(1, "first@x.com"),
            MakeUser(2, "second@x.com"),
            MakeUser(99, "admin@x.com"));
        await ctx.SaveChangesAsync();

        // First user pre-existing tenant with slug "shared"
        var firstReq = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("X", "shared", "111", null));
        await svc.ApproveAsync(firstReq, 99, new ApproveTenantRequestInput("X", "shared"));

        // Second user request, admin tries same slug
        var secondReq = await svc.CreateRequestAsync(2, new CreateTenantRequestInput("Y", "shared", "222", null));

        var act = () => svc.ApproveAsync(secondReq, 99, new ApproveTenantRequestInput("Y", "shared"));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Slug*");
    }

    [Fact]
    public async Task ApproveAsync_StatusNotPending_Throws()
    {
        var (ctx, svc, _, _) = Setup();
        ctx.Users.AddRange(MakeUser(1, "u@x.com"), MakeUser(99, "a@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Test", null, "111", null));
        await svc.CancelRequestAsync(id, 1);

        var act = () => svc.ApproveAsync(id, 99, new ApproveTenantRequestInput("Test", null));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*işlenmiş*");
    }

    [Fact]
    public async Task RejectAsync_HappyPath_StatusBecomesRejectedWithReason()
    {
        var (ctx, svc, emailMock, _) = Setup();
        ctx.Users.AddRange(MakeUser(1, "u@x.com"), MakeUser(99, "a@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Test", null, "111", null));

        await svc.RejectAsync(id, 99, "Eksik belge");

        var r = await ctx.TenantRequests.AsAdminQuery().FirstAsync(x => x.Id == id);
        r.Status.Should().Be(TenantRequestStatus.Rejected);
        r.RejectionReason.Should().Be("Eksik belge");
        r.ProcessedAt.Should().NotBeNull();
        r.ProcessedByUserId.Should().Be(99);

        emailMock.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_EmailModel_SiteUrlFromConfig()
    {
        var (ctx, svc, _, rendererMock) = Setup();
        ctx.Users.AddRange(MakeUser(1, "u@x.com"), MakeUser(99, "a@x.com"));
        await ctx.SaveChangesAsync();
        var reqId = await svc.CreateRequestAsync(1,
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

        await svc.ApproveAsync(reqId, 99, new ApproveTenantRequestInput("Hukuk Bürosu C", null));

        captured.Should().NotBeNull();
        captured!.SiteUrl.Should().Be(TestSiteUrl);
    }

    [Fact]
    public async Task RejectAsync_EmailModel_SiteUrlFromConfig()
    {
        var (ctx, svc, _, rendererMock) = Setup();
        ctx.Users.AddRange(MakeUser(1, "u@x.com"), MakeUser(99, "a@x.com"));
        await ctx.SaveChangesAsync();
        var id = await svc.CreateRequestAsync(1, new CreateTenantRequestInput("Reject Test", null, "111", null));

        LexCalculus.Core.Email.Models.TenantRequestRejectedModel? captured = null;
        rendererMock
            .Setup(r => r.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((_, model, _) =>
            {
                if (model is LexCalculus.Core.Email.Models.TenantRequestRejectedModel m)
                    captured = m;
            })
            .ReturnsAsync("<html></html>");

        await svc.RejectAsync(id, 99, "Eksik belge");

        captured.Should().NotBeNull();
        captured!.SiteUrl.Should().Be(TestSiteUrl);
    }
}
