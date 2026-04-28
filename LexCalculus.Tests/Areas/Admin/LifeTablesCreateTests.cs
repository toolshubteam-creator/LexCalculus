using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Areas.Admin;

[Collection("AdminWebHost")]
public class LifeTablesCreateTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public LifeTablesCreateTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient()
    {
        var options = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        };
        var client = _factory.CreateClient(options);
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private static string Build200RowValidCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Yas,Cinsiyet,BekledigiYasam");
        for (int yas = 0; yas <= 99; yas++)
        {
            var erkek = (80 - yas * 0.7m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var kadin = (84 - yas * 0.7m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine($"{yas},Erkek,{erkek}");
            sb.AppendLine($"{yas},Kadın,{kadin}");
        }
        return sb.ToString();
    }

    private async Task ClearTestTableAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await ctx.Set<LifeTable>().Where(t => t.Code == code).ToListAsync();
        if (existing.Count > 0)
        {
            ctx.Set<LifeTable>().RemoveRange(existing);
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Post_New_WithValidCsv_CreatesTableAndRedirects()
    {
        const string testCode = "TEST-CREATE-2026";
        await ClearTestTableAsync(testCode);

        var client = CreateAdminClient();
        var token = await GetAntiforgeryTokenAsync(client, "/admin/lifetable/yeni");

        var csv = Build200RowValidCsv();
        var csvBytes = Encoding.UTF8.GetBytes(csv);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent(testCode), "Code");
        form.Add(new StringContent("Test Tablosu 2026"), "Name");
        form.Add(new StringContent("2026-01-01"), "EffectiveDate");
        form.Add(new StringContent("Test Source"), "Source");
        form.Add(new StringContent("Integration test"), "Note");

        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "CsvFile", "test.csv");

        var response = await client.PostAsync("/admin/lifetable/yeni", form);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        // DB verification
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = await ctx.Set<LifeTable>()
            .Include(t => t.Rows)
            .FirstOrDefaultAsync(t => t.Code == testCode);

        created.Should().NotBeNull();
        created!.Code.Should().Be(testCode);
        created.IsActive.Should().BeFalse();    // Yeni tablo PASİF olarak eklenir
        created.Rows.Count.Should().Be(200);
    }
}
