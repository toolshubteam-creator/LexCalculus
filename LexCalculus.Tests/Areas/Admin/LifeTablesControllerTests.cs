using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class LifeTablesControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public LifeTablesControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task EnsureSeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var hasAny = await ctx.Set<LifeTable>().AnyAsync(t => t.Code == "TRH-2010-TEST");
        if (!hasAny)
        {
            ctx.Set<LifeTable>().Add(new LifeTable
            {
                Code = "TRH-2010-TEST",
                Name = "Test Hayat Tablosu 2010",
                EffectiveDate = new DateTime(2010, 1, 1),
                Source = "Test",
                IsActive = true
            });
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsListWithSeededTable()
    {
        await EnsureSeedAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/admin/lifetable");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // ASCII-stable substrings (CLAUDE.md: Türkçe HTML-encoded)
        body.Should().Contain("Hayat");                  // page title (Tablolar&#x131;)
        body.Should().Contain("TRH-2010-TEST");          // seeded code
        body.Should().Contain("param-table__row--active"); // aktif highlight
        body.Should().Contain("Aktif");                  // status badge text (ASCII)
    }
}
