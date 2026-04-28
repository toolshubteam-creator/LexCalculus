using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LexCalculus.Tests.Integration;

[CollectionDefinition("AdminWebHost", DisableParallelization = true)]
public class AdminWebHostCollection { }

/// <summary>
/// Anonymous access to /admin must trigger Identity cookie challenge → 302 to login.
/// Uses the regular cookie-auth factory.
/// </summary>
[Collection("AdminWebHost")]
public class AdminAnonymousAccessTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly WebApplicationFactoryFixture _factory;

    public AdminAnonymousAccessTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_GET_Admin_Redirects_To_Login()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/admin");

        ((int)response.StatusCode).Should().Be(302);
        response.Headers.Location?.OriginalString.Should().Contain("/Identity/Account/Login");
    }
}

/// <summary>
/// Authorized access tests using TestAuthHandler scheme.
/// Set X-Test-User header for identity; X-Test-Roles for roles.
/// </summary>
[Collection("AdminWebHost")]
public class AdminRoleAuthorizationTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public AdminRoleAuthorizationTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NonAdmin_User_GET_Admin_Returns_Forbidden()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add("X-Test-User", "test-avukat@local");
        // No X-Test-Roles header → user is authenticated but lacks Admin role

        var response = await client.GetAsync("/admin");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Admin_User_GET_Admin_Returns_OK_With_Dashboard()
    {
        var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "test-admin@local");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/admin");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Yönetim Paneli");
        body.Should().Contain("admin-sidebar");
    }
}
