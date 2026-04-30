using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Identity;

[Collection("AdminWebHost")]
public class ChangePasswordPageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public ChangePasswordPageTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId, string email, bool allowAutoRedirect = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found at {url}");
        return match.Groups[1].Value;
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null) await um.DeleteAsync(existing);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "ChangePassword Tester",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };
        var result = await um.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue();
        return user;
    }

    [Fact]
    public async Task Get_ChangePassword_Authenticated_Returns200()
    {
        var user = await CreateUserAsync("cp-get@example.com", "OldPass123!");
        using var client = CreateAuthClient(user.Id, user.Email!);

        var response = await client.GetAsync("/Identity/Account/Manage/ChangePassword");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Mevcut").And.Contain("Yeni");
    }

    [Fact]
    public async Task Post_ChangePassword_HappyPath_PasswordChangedAndRedirect()
    {
        const string oldPassword = "OldPass123!";
        const string newPassword = "NewPass456!";
        var user = await CreateUserAsync("cp-post@example.com", oldPassword);
        using var client = CreateAuthClient(user.Id, user.Email!, allowAutoRedirect: false);

        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Manage/ChangePassword");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.OldPassword", oldPassword),
            new KeyValuePair<string, string>("Input.NewPassword", newPassword),
            new KeyValuePair<string, string>("Input.ConfirmPassword", newPassword)
        });

        var response = await client.PostAsync("/Identity/Account/Manage/ChangePassword", form);
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
            "happy path PRG ile aynı sayfaya redirect bekleniyor");

        // Yeni şifreyle CheckPasswordAsync doğrulama
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = await um.FindByEmailAsync(user.Email!);
        refreshed.Should().NotBeNull();
        var newOk = await um.CheckPasswordAsync(refreshed!, newPassword);
        newOk.Should().BeTrue();
        var oldStillWorks = await um.CheckPasswordAsync(refreshed!, oldPassword);
        oldStillWorks.Should().BeFalse();
    }

    [Fact]
    public async Task Post_ChangePassword_WrongOldPassword_ReturnsValidationError()
    {
        const string oldPassword = "RealOld123!";
        var user = await CreateUserAsync("cp-wrong@example.com", oldPassword);
        using var client = CreateAuthClient(user.Id, user.Email!, allowAutoRedirect: false);

        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Manage/ChangePassword");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.OldPassword", "WrongOld123!"),
            new KeyValuePair<string, string>("Input.NewPassword", "BrandNew123!"),
            new KeyValuePair<string, string>("Input.ConfirmPassword", "BrandNew123!")
        });

        var response = await client.PostAsync("/Identity/Account/Manage/ChangePassword", form);
        // Page() döner — 200 + validation summary'de hata
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Eski şifre hâlâ geçerli olmalı
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshed = await um.FindByEmailAsync(user.Email!);
        var oldStillWorks = await um.CheckPasswordAsync(refreshed!, oldPassword);
        oldStillWorks.Should().BeTrue();
    }
}
