using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace LexCalculus.Tests.Services;

public class UserAdminServiceTests
{
    private static UserManager<ApplicationUser> MockUserManager(ApplicationDbContext ctx)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(x => x.Users).Returns(ctx.Users);
        return mgr.Object;
    }

    private static UserAdminService CreateService(ApplicationDbContext ctx)
        => new(
            ctx,
            MockUserManager(ctx),
            new Mock<LexCalculus.Core.Email.IEmailService>().Object,
            new Mock<LexCalculus.Core.Email.IEmailTemplateRenderer>().Object,
            new NullActivityLogService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UserAdminService>.Instance);

    private static ApplicationUser MakeUser(int id, string email, bool isActive = true) =>
        new()
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = email,
            CreatedAt = DateTime.UtcNow.AddDays(-id),
            IsActive = isActive,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

    private static ApplicationRole MakeRole(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant()
        };

    [Fact]
    public async Task GetUsersAsync_ReturnsAllUsersWithRoles()
    {
        await using var ctx = TestDbContextFactory.Create();

        var adminUser = MakeUser(1, "admin@test.local");
        var plainUser = MakeUser(2, "user@test.local");
        ctx.Users.AddRange(adminUser, plainUser);

        var adminRole = MakeRole(1, "Admin");
        ctx.Roles.Add(adminRole);

        ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = adminUser.Id, RoleId = adminRole.Id });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page = await svc.GetUsersAsync(1, 25);

        page.TotalCount.Should().Be(2);
        page.Items.Should().HaveCount(2);

        var adminItem = page.Items.Single(u => u.Email == "admin@test.local");
        adminItem.RoleName.Should().Be("Admin");

        var plainItem = page.Items.Single(u => u.Email == "user@test.local");
        plainItem.RoleName.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_FilterByRole_ReturnsOnlyMatching()
    {
        await using var ctx = TestDbContextFactory.Create();

        var adminUser = MakeUser(1, "admin@test.local");
        var u1 = MakeUser(2, "u1@test.local");
        var u2 = MakeUser(3, "u2@test.local");
        ctx.Users.AddRange(adminUser, u1, u2);

        var adminRole = MakeRole(1, "Admin");
        var kullaniciRole = MakeRole(2, "Kullanici");
        ctx.Roles.AddRange(adminRole, kullaniciRole);

        ctx.UserRoles.AddRange(
            new IdentityUserRole<int> { UserId = adminUser.Id, RoleId = adminRole.Id },
            new IdentityUserRole<int> { UserId = u1.Id, RoleId = kullaniciRole.Id },
            new IdentityUserRole<int> { UserId = u2.Id, RoleId = kullaniciRole.Id });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page = await svc.GetUsersAsync(1, 25, roleFilter: "Kullanici");

        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(u => u.RoleName == "Kullanici");
    }

    [Fact]
    public async Task GetUsersAsync_FilterByIsActive_ReturnsOnlyMatching()
    {
        await using var ctx = TestDbContextFactory.Create();

        var active = MakeUser(1, "active@test.local", isActive: true);
        var passive = MakeUser(2, "passive@test.local", isActive: false);
        ctx.Users.AddRange(active, passive);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var page = await svc.GetUsersAsync(1, 25, isActiveFilter: false);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Email.Should().Be("passive@test.local");
    }
}
