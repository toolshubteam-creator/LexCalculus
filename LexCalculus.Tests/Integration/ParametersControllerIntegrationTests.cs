using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Integration;

[Collection("AdminWebHost")]
public class ParametersControllerIntegrationTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public ParametersControllerIntegrationTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedAsync(params FormulaParameter[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Clear any existing rows from prior tests to keep assertions deterministic
        ctx.Set<FormulaParameter>().RemoveRange(ctx.Set<FormulaParameter>());
        await ctx.SaveChangesAsync();
        ctx.Set<FormulaParameter>().AddRange(rows);
        await ctx.SaveChangesAsync();
    }

    private HttpClient CreateAdminClient(bool followRedirects = true)
    {
        var client = followRedirects
            ? _factory.CreateClient()
            : _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsOk_AndContainsParametersHeading()
    {
        await SeedAsync(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 50000m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 26000m, EffectiveDate = new DateTime(2025, 1, 1) }
        );

        var client = CreateAdminClient();

        var response = await client.GetAsync("/admin/parametreler");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Parametreler");
        body.Should().Contain("kidem-tazminati");
    }

    [Fact]
    public async Task Index_WithToolSlugFilter_FiltersResults()
    {
        await SeedAsync(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 50000m, EffectiveDate = new DateTime(2025, 1, 1) },
            new FormulaParameter { ToolSlug = "*", Key = "asgari-ucret-brut", Value = 26000m, EffectiveDate = new DateTime(2025, 1, 1) }
        );

        var client = CreateAdminClient();

        var response = await client.GetAsync("/admin/parametreler?ToolSlug=*");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("asgari-ucret-brut");
        body.Should().Contain("Global");
        // Tool-specific row should be filtered out
        body.Should().NotContain("<code class=\"param-table__slug\">kidem-tazminati</code>");
    }

    [Fact]
    public async Task History_AsAdmin_ReturnsVersionsForGivenSlugAndKey()
    {
        await SeedAsync(
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 35058.58m, EffectiveDate = new DateTime(2024, 1, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 41828.42m, EffectiveDate = new DateTime(2024, 7, 1) },
            new FormulaParameter { ToolSlug = "kidem-tazminati", Key = "tavan", Value = 53919.68m, EffectiveDate = new DateTime(2026, 1, 1) }
        );

        var client = CreateAdminClient();

        var response = await client.GetAsync("/admin/parametreler/gecmis/kidem-tazminati/tavan");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Parametre Tarihçesi");
        body.Should().Contain("kidem-tazminati");
        body.Should().Contain("3 versiyon");
        body.Should().Contain("Güncel");  // most recent version badge
    }
}
