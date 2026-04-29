using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class UsersControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public UsersControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await ctx.Users.AnyAsync(u => u.Email == "admin@lexcalculus.local"))
            return;

        var role = await ctx.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (role == null)
        {
            role = new ApplicationRole { Name = "Admin", NormalizedName = "ADMIN" };
            ctx.Roles.Add(role);
            await ctx.SaveChangesAsync();
        }

        var user = new ApplicationUser
        {
            UserName = "admin@lexcalculus.local",
            NormalizedUserName = "ADMIN@LEXCALCULUS.LOCAL",
            Email = "admin@lexcalculus.local",
            NormalizedEmail = "ADMIN@LEXCALCULUS.LOCAL",
            FullName = "System Administrator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsListWithSeededAdmin()
    {
        await EnsureSeedAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/admin/kullanicilar");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // ASCII-stable substrings (CLAUDE.md: Türkçe HTML-encoded)
        body.Should().Contain("Kullan");                     // "Kullanıcılar" page title
        body.Should().Contain("admin@lexcalculus.local");    // seeded admin email
        body.Should().Contain("param-badge--admin");         // admin role badge class
    }
}
