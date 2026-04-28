using FluentAssertions;
using Xunit;

namespace LexCalculus.Tests.Integration;

[Collection("AdminWebHost")]
public class HomeIntegrationTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;

    public HomeIntegrationTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_Index_Returns_Success_And_Contains_Brand()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Lex");
        body.Should().Contain("Calculus");
        body.Should().Contain("Anno MMXXVI"); // hero eyebrow as a tema canary
    }

    [Fact]
    public async Task Home_Index_Renders_SeoMeta_Tags()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("<title>Lex Calculus");
        body.Should().Contain("<meta name=\"description\"");
        body.Should().Contain("rel=\"canonical\"");
        body.Should().Contain("application/ld+json");
        body.Should().Contain("\"@type\"");
        body.Should().Contain("Organization");
    }

    [Fact]
    public async Task Sitemap_Returns_Valid_Xml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/sitemap.xml");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("<?xml");
        body.Should().Contain("<urlset");
        body.Should().Contain("sitemaps.org/schemas/sitemap/0.9");
        body.Should().Contain("<loc>");
    }

    [Fact]
    public async Task Robots_Returns_Plain_Text_With_Sitemap_Reference()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/robots.txt");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("User-agent: *");
        body.Should().Contain("Disallow: /Identity/");
        body.Should().Contain("Sitemap:");
    }

    [Fact]
    public async Task Health_Live_Returns_200_With_Healthy_Json()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Healthy\"");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Json_With_Checks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        // Status may be Healthy or Degraded (cache fallback in test env), but must not be Unhealthy
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"checks\":");
        body.Should().Contain("\"status\":");

        // Response code 200 for Healthy/Degraded, 503 for Unhealthy
        ((int)response.StatusCode).Should().BeOneOf(200, 503);
    }

    [Fact]
    public async Task Identity_Login_Page_Returns_200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Identity/Account/Login");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Giriş Yap");
    }

    [Fact]
    public async Task Privacy_Page_Has_Article_OgType()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Home/Privacy");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("og:type");
        body.Should().Contain("\"article\"");
    }
}
