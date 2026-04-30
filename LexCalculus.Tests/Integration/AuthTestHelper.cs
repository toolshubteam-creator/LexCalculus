using System.Security.Claims;
using System.Text.Encodings.Web;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Data.Interceptors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

/// <summary>
/// Test factory that uses TestAuthHandler instead of cookie auth.
/// Authentication is driven by request headers (see TestAuthHandler).
/// </summary>
public sealed class TestAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"LexCalculusTest_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "");
        builder.UseSetting("Testing", "true");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.AddInterceptors(new AuditInterceptor());
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
                options.DefaultSignOutScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
