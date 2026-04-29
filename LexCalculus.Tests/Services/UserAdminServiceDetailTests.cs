using FluentAssertions;
using LexCalculus.Core.Email;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Services;

public class UserAdminServiceDetailTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager(ApplicationDbContext ctx)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(x => x.Users).Returns(ctx.Users);
        return mgr;
    }

    private static UserAdminService CreateService(
        ApplicationDbContext ctx,
        Mock<UserManager<ApplicationUser>>? umMock = null,
        Mock<IEmailService>? emailMock = null,
        Mock<IEmailTemplateRenderer>? rendererMock = null)
    {
        var um = umMock?.Object ?? MockUserManager(ctx).Object;
        var email = emailMock?.Object ?? new Mock<IEmailService>().Object;
        var renderer = rendererMock?.Object ?? new Mock<IEmailTemplateRenderer>().Object;
        return new UserAdminService(ctx, um, email, renderer, NullLogger<UserAdminService>.Instance);
    }

    [Fact]
    public async Task GetUserDetailAsync_ReturnsCompleteData()
    {
        await using var ctx = TestDbContextFactory.Create();

        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "test@example.com",
            NormalizedUserName = "TEST@EXAMPLE.COM",
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            FullName = "Test User",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            IsActive = true,
            EmailConfirmed = true,
            PhoneNumber = "5551234567",
            SecurityStamp = Guid.NewGuid().ToString(),
            Profile = new UserProfile
            {
                UserId = 1,
                DisplayName = "Test User",
                MeslekTuru = MeslekTuru.Avukat,
                BaroNo = "12345"
            }
        };
        ctx.Users.Add(user);

        var role = new ApplicationRole { Id = 10, Name = "Kullanici", NormalizedName = "KULLANICI" };
        ctx.Roles.Add(role);
        ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = 1, RoleId = 10 });

        for (int i = 0; i < 5; i++)
        {
            ctx.CalculationHistories.Add(new CalculationHistory
            {
                UserId = 1,
                ToolSlug = $"tool-{i}",
                ToolTitle = $"Tool {i}",
                CategorySlug = "test",
                InputJson = "{}",
                OutputJson = "{}",
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await ctx.SaveChangesAsync();

        var umMock = MockUserManager(ctx);
        umMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "Kullanici" });

        var svc = CreateService(ctx, umMock);
        var detail = await svc.GetUserDetailAsync(1);

        detail.Should().NotBeNull();
        detail!.Email.Should().Be("test@example.com");
        detail.FullName.Should().Be("Test User");
        detail.RoleName.Should().Be("Kullanici");
        detail.MeslekTuruLabel.Should().Be("Avukat");
        detail.BaroNo.Should().Be("12345");
        detail.PhoneNumber.Should().Be("5551234567");
        detail.RecentCalculations.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetUserDetailAsync_NonexistentUser_ReturnsNull()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);
        var detail = await svc.GetUserDetailAsync(999);
        detail.Should().BeNull();
    }

    [Fact]
    public async Task ChangeRoleAsync_InvalidRole_ReturnsFalse()
    {
        await using var ctx = TestDbContextFactory.Create();
        var svc = CreateService(ctx);

        var ok = await svc.ChangeRoleAsync(1, "GeçersizRol");

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_GeneratesTokenAndSendsEmail()
    {
        await using var ctx = TestDbContextFactory.Create();

        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "reset@example.com",
            NormalizedUserName = "RESET@EXAMPLE.COM",
            Email = "reset@example.com",
            NormalizedEmail = "RESET@EXAMPLE.COM",
            FullName = "Reset Test",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var umMock = MockUserManager(ctx);
        umMock.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
        umMock.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("fake-token");

        var rendererMock = new Mock<IEmailTemplateRenderer>();
        rendererMock
            .Setup(x => x.RenderAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>reset body</html>");

        EmailMessage? captured = null;
        var emailMock = new Mock<IEmailService>();
        emailMock
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(true);

        var svc = CreateService(ctx, umMock, emailMock, rendererMock);
        var ok = await svc.SendPasswordResetEmailAsync(1, "https://test.local");

        ok.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.ToAddress.Should().Be("reset@example.com");
        captured.Subject.Should().Contain("ifre");  // "Şifre" ASCII-stable suffix
        captured.HtmlBody.Should().Contain("reset body");
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_NonexistentUser_ReturnsFalse()
    {
        await using var ctx = TestDbContextFactory.Create();

        var umMock = MockUserManager(ctx);
        umMock.Setup(x => x.FindByIdAsync("999")).ReturnsAsync((ApplicationUser?)null);

        var svc = CreateService(ctx, umMock);
        var ok = await svc.SendPasswordResetEmailAsync(999, "https://test.local");

        ok.Should().BeFalse();
    }
}
