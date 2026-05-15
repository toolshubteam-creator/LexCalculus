using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCalculus.Tests.Integration;

/// <summary>
/// Test authentication handler that synthesizes a ClaimsPrincipal from request headers.
/// Send "X-Test-User" for user name; "X-Test-Roles" comma-separated for roles.
/// If no header → unauthenticated (NoResult).
///
/// IAuthenticationSignInHandler implementation no-op: production kod
/// SignInManager.SignInAsync / RefreshSignInAsync çağırırken default sign-in
/// scheme bu interface'i sunmadığında exception fırlardı. Tech-debt madde 5.
///
/// Not: Adım 5.8 P2'de InMemory varyantı (TestAuthWebApplicationFactory) kaldırıldı;
/// integration testler artık <see cref="SqlServerTestAuthWebApplicationFactory"/>
/// üzerinden bu handler'ı kullanıyor.
/// </summary>
public sealed class TestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationSignInHandler
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userName))
            return Task.FromResult(AuthenticateResult.NoResult());

        var nameIdentifier = Request.Headers.TryGetValue("X-Test-UserId", out var uid)
            ? uid.ToString()
            : "1";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName.ToString()),
            new(ClaimTypes.NameIdentifier, nameIdentifier)
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
        {
            foreach (var role in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        => Task.CompletedTask;

    public Task SignOutAsync(AuthenticationProperties? properties)
        => Task.CompletedTask;
}
