using FluentAssertions;
using LexCalculus.Tests.Integration;
using Xunit;

namespace LexCalculus.Tests.Seo;

[Collection("AdminWebHost")]
public class SeoHygieneTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public SeoHygieneTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HesaplarimIndex_AsAuthenticatedUser_RendersNoIndexMetaTag()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "test-user@local");

        var response = await client.GetAsync("/hesaplarim");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<meta name=\"robots\" content=\"noindex,nofollow\"");
    }

    [Fact]
    public async Task RobotsTxt_ReturnsDisallowForPrivatePaths()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/robots.txt");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Disallow: /hesaplarim");
        body.Should().Contain("Disallow: /bildirimler");
        body.Should().Contain("Sitemap:");
    }
}
