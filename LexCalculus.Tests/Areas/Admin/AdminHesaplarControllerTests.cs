using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class AdminHesaplarControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public AdminHesaplarControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task ClearAndSeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ctx.Set<CalculationHistory>().RemoveRange(
            ctx.Set<CalculationHistory>().IgnoreQueryFilters());
        // Mevcut Users'a dokunma — fixture instance arasında share var
        await ctx.SaveChangesAsync();

        // 2 user seed (Identity üzerinden değil direkt EF üzerinden, test amaçlı)
        var existingIds = await ctx.Users.Select(u => u.Id).ToListAsync();
        if (!existingIds.Contains(101))
        {
            ctx.Users.Add(new ApplicationUser
            {
                Id = 101, UserName = "alice@test.local", NormalizedUserName = "ALICE@TEST.LOCAL",
                Email = "alice@test.local", NormalizedEmail = "ALICE@TEST.LOCAL",
                FullName = "Alice Demo", IsActive = true, SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        if (!existingIds.Contains(102))
        {
            ctx.Users.Add(new ApplicationUser
            {
                Id = 102, UserName = "bob@test.local", NormalizedUserName = "BOB@TEST.LOCAL",
                Email = "bob@test.local", NormalizedEmail = "BOB@TEST.LOCAL",
                FullName = "Bob Demo", IsActive = true, SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        ctx.Set<CalculationHistory>().AddRange(
            new CalculationHistory
            {
                UserId = 101, CategorySlug = "is-hukuku", ToolSlug = "kidem-tazminati",
                ToolTitle = "Kıdem Tazminatı", InputJson = "{}", OutputJson = "{}",
                TotalAmount = 12345m, Unit = "TL"
            },
            new CalculationHistory
            {
                UserId = 102, CategorySlug = "faiz", ToolSlug = "yasal-faiz",
                ToolTitle = "Yasal Faiz", InputJson = "{}", OutputJson = "{}",
                TotalAmount = 543m, Unit = "TL"
            }
        );
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsListWithUsersJoined()
    {
        await ClearAndSeedAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/admin/hesaplar");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // ASCII-stable substrings (CLAUDE.md: Türkçe HTML-encoded olur)
        body.Should().Contain("Hesap");                        // page title
        body.Should().Contain("alice@test.local");             // user A
        body.Should().Contain("bob@test.local");               // user B
        body.Should().Contain("Yasal Faiz");                   // ASCII tool title
        body.Should().Contain("dem Tazminat");                 // K&#x131;dem Tazminat&#x131; (encoded)
        body.Should().Contain("2 toplam hesap");
    }
}
