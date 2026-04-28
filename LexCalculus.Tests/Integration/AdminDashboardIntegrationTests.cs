using FluentAssertions;
using Xunit;

namespace LexCalculus.Tests.Integration;

[Collection("AdminWebHost")]
public class AdminDashboardIntegrationTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public AdminDashboardIntegrationTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Index_AsAdmin_ReturnsDashboard_WithAllWidgetCaptions()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/admin");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // Sayfa başlığı + 5 widget caption + grid container
        body.Should().Contain("Yönetim Paneli");
        body.Should().Contain("dashboard-grid");
        body.Should().Contain("Veri Tazelik");
        body.Should().Contain("Son 7 Gün");
        body.Should().Contain("Kullanıcılar");
        body.Should().Contain("Background Jobs");
        body.Should().Contain("Bildirimler");
    }
}
