using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Integration;

[Collection("AdminWebHost")]
public class ParametersControllerCrudTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public ParametersControllerCrudTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task ClearAndSeedAsync(params FormulaParameter[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Set<FormulaParameter>().RemoveRange(ctx.Set<FormulaParameter>().IgnoreQueryFilters());
        await ctx.SaveChangesAsync();
        if (rows.Length > 0)
        {
            ctx.Set<FormulaParameter>().AddRange(rows);
            await ctx.SaveChangesAsync();
        }
    }

    private HttpClient CreateAdminClient() => CreateAdminClient(allowAutoRedirect: false);

    private HttpClient CreateAdminClient(bool allowAutoRedirect)
    {
        var options = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        };
        var client = _factory.CreateClient(options);
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    /// <summary>
    /// GET a form page and extract the antiforgery token. The cookie is captured
    /// automatically by HttpClient's cookie container.
    /// </summary>
    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task Get_New_AsAdmin_ReturnsForm()
    {
        await ClearAndSeedAsync();
        var client = CreateAdminClient(allowAutoRedirect: true);

        var response = await client.GetAsync("/admin/parametreler/yeni");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Yeni Parametre");
        body.Should().Contain("admin-form");
    }

    [Fact]
    public async Task Post_New_WithValidData_PersistsAndRedirects()
    {
        await ClearAndSeedAsync();
        var client = CreateAdminClient();

        var token = await GetAntiforgeryTokenAsync(client, "/admin/parametreler/yeni");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Mode", "New"),
            new KeyValuePair<string, string>("ToolSlug", "test-tool"),
            new KeyValuePair<string, string>("Key", "test-key"),
            new KeyValuePair<string, string>("Value", "42.5"),
            new KeyValuePair<string, string>("EffectiveDate", "2026-04-28"),
            new KeyValuePair<string, string>("ExpectedUpdateFrequency", "Yearly"),
            new KeyValuePair<string, string>("LastUpdatedDate", "2026-04-28"),
            new KeyValuePair<string, string>("Source", "Phase 3.2 test"),
            new KeyValuePair<string, string>("Note", ""),
            new KeyValuePair<string, string>("Notes", "")
        });

        var response = await client.PostAsync("/admin/parametreler/yeni", form);

        ((int)response.StatusCode).Should().Be(302);
        response.Headers.Location?.OriginalString.Should().Contain("/admin/parametreler");

        // Verify DB
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await ctx.Set<FormulaParameter>()
            .FirstOrDefaultAsync(p => p.ToolSlug == "test-tool" && p.Key == "test-key");
        saved.Should().NotBeNull();
        saved!.Value.Should().Be(42.5m);
        saved.CreatedByUserId.Should().Be(1); // TestAuthHandler sets NameIdentifier=1
    }

    [Fact]
    public async Task Post_New_WithDuplicateKey_ReturnsValidationError()
    {
        var existing = new FormulaParameter
        {
            ToolSlug = "dup-tool",
            Key = "dup-key",
            Value = 100m,
            EffectiveDate = new DateTime(2026, 1, 1)
        };
        await ClearAndSeedAsync(existing);

        var client = CreateAdminClient();
        var token = await GetAntiforgeryTokenAsync(client, "/admin/parametreler/yeni");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Mode", "New"),
            new KeyValuePair<string, string>("ToolSlug", "dup-tool"),
            new KeyValuePair<string, string>("Key", "dup-key"),
            new KeyValuePair<string, string>("Value", "200"),
            new KeyValuePair<string, string>("EffectiveDate", "2026-01-01"),
            new KeyValuePair<string, string>("ExpectedUpdateFrequency", "Yearly"),
            new KeyValuePair<string, string>("LastUpdatedDate", "2026-01-01"),
            new KeyValuePair<string, string>("Source", "duplicate test"),
            new KeyValuePair<string, string>("Note", ""),
            new KeyValuePair<string, string>("Notes", "")
        });

        var response = await client.PostAsync("/admin/parametreler/yeni", form);

        // Validation error → form re-rendered with 200, no redirect
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Turkish chars get HTML-encoded ("ı" → "&#x131;"); look for ASCII-safe substrings
        body.Should().Contain("validation-summary-errors");
        body.Should().Contain("dup-tool");
        body.Should().Contain("dup-key");
    }

    [Fact]
    public async Task Post_Delete_AsAdmin_SoftDeletesRow()
    {
        var seed = new FormulaParameter
        {
            ToolSlug = "del-tool",
            Key = "del-key",
            Value = 999m,
            EffectiveDate = new DateTime(2026, 1, 1)
        };
        await ClearAndSeedAsync(seed);

        // Reload to get the assigned Id
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seed = await ctx.Set<FormulaParameter>()
                .FirstAsync(p => p.ToolSlug == "del-tool" && p.Key == "del-key");
        }

        var client = CreateAdminClient();
        // Delete uses an antiforgery token from the Index page (form is in the table row)
        var indexHtml = await client.GetStringAsync("/admin/parametreler");
        var token = Regex.Match(indexHtml,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        token.Should().NotBeNullOrEmpty();

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });

        var response = await client.PostAsync($"/admin/parametreler/sil/{seed.Id}", form);

        ((int)response.StatusCode).Should().Be(302);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyCtx = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await verifyCtx.Set<FormulaParameter>()
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == seed.Id);
        deleted.IsDeleted.Should().BeTrue();
        deleted.LastModifiedByUserId.Should().Be(1);
    }
}
