using System.Net;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.SignalR;

/// <summary>
/// /hubs/messages negotiate endpoint auth davranışı (Faz 5.6, charter §3
/// Karar 8 — cookie auth). Anonim → 401; auth'lı → 200 (negotiate JSON).
/// Tam SignalR connection round-trip Faz 6+ end-to-end test'lerine bırakıldı.
/// </summary>
[Collection("AdminWebHost")]
public class MessagesHubAuthTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public MessagesHubAuthTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthClient(int userId, string email)
    {
        var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.Add("X-Test-User", email);
        c.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return c;
    }

    private async Task<ApplicationUser> SeedUserAsync(string email)
    {
        await CleanupUserAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Hub " + email,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        var r = await um.CreateAsync(u, "ValidPass123!");
        r.Succeeded.Should().BeTrue();
        return u;
    }

    private async Task CleanupUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await ctx.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null) return;
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == u.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == u.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(u);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task HubNegotiate_Anonymous_ReturnsUnauthorizedOrRedirect()
    {
        using var client = CreateAnonClient();
        // SignalR negotiate POST /hubs/messages/negotiate?negotiateVersion=1
        var resp = await client.PostAsync(
            "/hubs/messages/negotiate?negotiateVersion=1", content: null);

        // [Authorize] hub: anonim için Unauthorized veya redirect (login sayfasına)
        ((int)resp.StatusCode).Should().BeOneOf(
            (int)HttpStatusCode.Unauthorized,
            (int)HttpStatusCode.Redirect,
            (int)HttpStatusCode.Found);
    }

    [Fact]
    public async Task HubNegotiate_Authenticated_ReturnsOk()
    {
        var u = await SeedUserAsync("hub-neg@example.com");
        try
        {
            using var client = CreateAuthClient(u.Id, u.Email!);
            var resp = await client.PostAsync(
                "/hubs/messages/negotiate?negotiateVersion=1", content: null);

            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await resp.Content.ReadAsStringAsync();
            // Negotiate response: connectionId, availableTransports JSON
            body.Should().Contain("connectionId");
        }
        finally
        {
            await CleanupUserAsync(u.Email!);
        }
    }
}
