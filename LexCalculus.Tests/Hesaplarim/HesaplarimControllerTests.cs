using FluentAssertions;
using LexCalculus.Core.Entities;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Hesaplarim;

[Collection("AdminWebHost")]
public class HesaplarimControllerTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public HesaplarimControllerTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task ClearAndSeedAsync(params CalculationHistory[] rows)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Set<CalculationHistory>().RemoveRange(
            ctx.Set<CalculationHistory>().IgnoreQueryFilters());
        await ctx.SaveChangesAsync();
        if (rows.Length > 0)
        {
            ctx.Set<CalculationHistory>().AddRange(rows);
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Index_Anonymous_Returns401()
    {
        await ClearAndSeedAsync();
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        // X-Test-User header yok → unauthenticated → 401
        var response = await client.GetAsync("/hesaplarim");

        ((int)response.StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task Index_AsUser_ReturnsListView()
    {
        // TestAuthHandler NameIdentifier="1" hardcoded → userId=1
        await ClearAndSeedAsync(
            new CalculationHistory
            {
                UserId = 1, CategorySlug = "is-hukuku",
                ToolSlug = "kidem-tazminati",
                ToolTitle = "Kıdem Tazminatı",
                InputJson = "{}", OutputJson = "{}",
                TotalAmount = 12345.67m, Unit = "TL"
            },
            new CalculationHistory
            {
                UserId = 1, CategorySlug = "faiz",
                ToolSlug = "yasal-faiz",
                ToolTitle = "Yasal Faiz",
                InputJson = "{}", OutputJson = "{}",
                TotalAmount = 543.21m, Unit = "TL"
            }
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");

        var response = await client.GetAsync("/hesaplarim");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // Razor Turkish karakterleri HTML entity'lere encode eder (ı → &#x131;).
        // Bu normal davranış; içerik testi ASCII-stable parçalarla yapılır.
        body.Should().Contain("Hesaplar");           // header (Hesaplar&#x131;m)
        body.Should().Contain("dem Tazminat");       // K&#x131;dem Tazminat&#x131;
        body.Should().Contain("Yasal Faiz");         // ASCII-only
        body.Should().Contain("Toplam 2 hesap");
        body.Should().Contain("hesap-list");         // liste render edildi
    }
}
