using System.Net;
using FluentAssertions;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LexCalculus.Tests.Web;

/// <summary>
/// Faz 5.3 — _Layout otomatik NoIndex (charter Karar 12).
/// [Authorize] sayfa veya admin area otomatik noindex meta render eder.
/// </summary>
[Collection("AdminWebHost")]
public class NoIndexAutoTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public NoIndexAutoTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId = 9100, string email = "noindex@local")
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task AuthorizedPage_RendersNoIndexMeta()
    {
        // /baglantilarim — [Authorize] korumalı
        using var client = CreateAuthClient();
        var response = await client.GetAsync("/baglantilarim");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("noindex",
            "[Authorize] sayfa otomatik noindex meta render etmeli");
    }

    [Fact]
    public async Task AnonymousPublicPage_DoesNotRenderNoIndexMeta()
    {
        // / (Home) — anonim erişilebilir, noindex YOK
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("noindex,nofollow",
            "anonim public sayfa noindex meta render etmemeli");
    }
}
