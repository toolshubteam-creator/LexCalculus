using System.Text.Json;
using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Hesaplarim;

[Collection("AdminWebHost")]
public class HesaplarimDetailControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public HesaplarimDetailControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedHistoryAsync(int userId, string toolSlug, string toolTitle, string inputJson)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Set<CalculationHistory>().RemoveRange(
            ctx.Set<CalculationHistory>().IgnoreQueryFilters());
        await ctx.SaveChangesAsync();

        var entry = new CalculationHistory
        {
            UserId = userId,
            CategorySlug = "faiz",
            ToolSlug = toolSlug,
            ToolTitle = toolTitle,
            InputJson = inputJson,
            OutputJson = "{\"totalAmount\":11781.91}",
            TotalAmount = 11781.91m,
            Unit = "TL"
        };
        ctx.Set<CalculationHistory>().Add(entry);
        await ctx.SaveChangesAsync();
        return entry.Id;
    }

    [Fact]
    public async Task Detail_AsOwner_ReturnsView()
    {
        // TestAuthHandler NameIdentifier="1" hardcoded → userId=1
        var inputJson = JsonSerializer.Serialize(new
        {
            anaPara = 10000m,
            baslangicTarihi = "2024-01-01",
            hesapTarihi = "2024-12-31",
            gunYili = 0
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var historyId = await SeedHistoryAsync(userId: 1, toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz", inputJson: inputJson);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");

        var response = await client.GetAsync($"/hesaplarim/{historyId}");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // ASCII-stable substring'ler (CLAUDE.md kuralı: Türkçe HTML-encoded olur)
        body.Should().Contain("Yasal Faiz");
        body.Should().Contain("Sonu");                          // "Sonuç" → "Sonu&#xC7;" partial
        body.Should().Contain("hesap-detail__total");           // total kart render
        body.Should().Contain("Form");                          // "Form'u Tekrarla" button
        body.Should().Contain("/hesapla/faiz/yasal-faiz?restore=" + historyId);
    }

    [Fact]
    public async Task Detail_NotOwner_Returns404()
    {
        // History user=2'ye ait, biz user=1 olarak login
        var historyId = await SeedHistoryAsync(userId: 2, toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz", inputJson: "{}");

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");

        var response = await client.GetAsync($"/hesaplarim/{historyId}");

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task Restore_RoundTrip_PrefillsForm()
    {
        var inputJson = JsonSerializer.Serialize(new
        {
            anaPara = 10000m,
            baslangicTarihi = "2024-01-01",
            hesapTarihi = "2024-12-31",
            gunYili = 0
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var historyId = await SeedHistoryAsync(userId: 1, toolSlug: "yasal-faiz",
            toolTitle: "Yasal Faiz", inputJson: inputJson);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");

        var response = await client.GetAsync($"/hesapla/faiz/yasal-faiz?restore={historyId}");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // Form input value'ları render edildi mi (ana para 10000 + tarihler)
        body.Should().Contain("10000");
        body.Should().Contain("2024-01-01");
        body.Should().Contain("2024-12-31");
    }
}
