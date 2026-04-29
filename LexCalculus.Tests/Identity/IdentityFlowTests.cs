using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Identity;

[Collection("AdminWebHost")]
public class IdentityFlowTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public IdentityFlowTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password,
        bool isActive = true,
        bool emailConfirmed = true)
    {
        await RemoveUserIfExistsAsync(email);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Flow Test",
            CreatedAt = DateTime.UtcNow,
            IsActive = isActive,
            EmailConfirmed = emailConfirmed
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(
            $"seed user creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    private async Task RemoveUserIfExistsAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return;
        var profile = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);
        var roles = ctx.UserRoles.Where(ur => ur.UserId == user.Id);
        ctx.UserRoles.RemoveRange(roles);
        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_SetsEmailConfirmedTrue()
    {
        const string email = "confirmtest@example.com";
        var user = await CreateUserAsync(email, "ValidPass123!", emailConfirmed: false);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var fetched = await um.FindByEmailAsync(email);
            fetched.Should().NotBeNull();
            token = await um.GenerateEmailConfirmationTokenAsync(fetched!);
        }
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var client = CreateClient(allowAutoRedirect: true);
        var url = $"/Identity/Account/ConfirmEmail?userId={user.Id}&code={Uri.EscapeDataString(encodedCode)}";
        var response = await client.GetAsync(url);

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        // Türkçe encoding'e karşı dayanıklı: success branch'ında render edilen class
        body.Should().Contain("form-section__message--success");

        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var refreshed = await um.FindByEmailAsync(email);
            refreshed!.EmailConfirmed.Should().BeTrue();
        }

        await RemoveUserIfExistsAsync(email);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_ShowsError()
    {
        const string email = "badtoken@example.com";
        var user = await CreateUserAsync(email, "ValidPass123!", emailConfirmed: false);

        var fakeToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-valid-token"));
        var client = CreateClient(allowAutoRedirect: true);
        var url = $"/Identity/Account/ConfirmEmail?userId={user.Id}&code={Uri.EscapeDataString(fakeToken)}";

        var response = await client.GetAsync(url);

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("form-section__message--error");
        body.Should().Contain("Yeniden");

        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var refreshed = await um.FindByEmailAsync(email);
            refreshed!.EmailConfirmed.Should().BeFalse("invalid token must not confirm email");
        }

        await RemoveUserIfExistsAsync(email);
    }

    [Fact]
    public async Task Login_InactiveUser_IsRejected()
    {
        const string email = "inactive@example.com";
        await CreateUserAsync(email, "ValidPass123!", isActive: false, emailConfirmed: true);

        var client = CreateClient(allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", "ValidPass123!"),
            new KeyValuePair<string, string>("Input.RememberMe", "false")
        });

        var response = await client.PostAsync("/Identity/Account/Login", form);

        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Hesab"); // "Hesabınız askıya alınmış" — ASCII-safe prefix

        await RemoveUserIfExistsAsync(email);
    }

    [Fact]
    public async Task Login_UnconfirmedUser_IsRejected()
    {
        const string email = "unconfirmed@example.com";
        await CreateUserAsync(email, "ValidPass123!", isActive: true, emailConfirmed: false);

        var client = CreateClient(allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.Password", "ValidPass123!"),
            new KeyValuePair<string, string>("Input.RememberMe", "false")
        });

        var response = await client.PostAsync("/Identity/Account/Login", form);

        // RequireConfirmedAccount=true → PasswordSignInAsync result.IsNotAllowed
        // Mevcut Login.cshtml.cs IsNotAllowed branch'ı yok → "Invalid login attempt" generic hata, Page() döner.
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid login attempt");

        await RemoveUserIfExistsAsync(email);
    }
}
