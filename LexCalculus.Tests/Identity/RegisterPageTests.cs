using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Enums;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Identity;

[Collection("AdminWebHost")]
public class RegisterPageTests : IClassFixture<TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public RegisterPageTests(TestAuthWebApplicationFactory factory)
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

    private async Task RemoveUserIfExistsAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return;

        var profile = await ctx.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (profile is not null) ctx.UserProfiles.Remove(profile);

        var roles = ctx.UserRoles.Where(ur => ur.UserId == user.Id);
        ctx.UserRoles.RemoveRange(roles);

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_Register_ReturnsFormWithMeslekDropdown()
    {
        var client = CreateClient(allowAutoRedirect: true);

        var response = await client.GetAsync("/Identity/Account/Register");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();

        // ASCII-stable substrings (CLAUDE.md: Türkçe HTML-encoded)
        body.Should().Contain("Mesle");        // "Mesleğiniz" label
        body.Should().Contain("Avukat");       // option value=1 metni
        body.Should().Contain("Bilirki");      // "Bilirkişi" option (ASCII prefix)
        body.Should().Contain("Input_MeslekTuru"); // dropdown id
        body.Should().Contain("Ad-Soyad");     // FullName label
    }

    [Fact]
    public async Task Post_Register_HappyPath_CreatesUserWithProfileAndRole()
    {
        const string email = "registertest@example.com";
        await RemoveUserIfExistsAsync(email);

        var client = CreateClient(allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Register");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.FullName", "Register Test"),
            new KeyValuePair<string, string>("Input.Password", "ValidPass123!"),
            new KeyValuePair<string, string>("Input.ConfirmPassword", "ValidPass123!"),
            new KeyValuePair<string, string>("Input.MeslekTuru", "1"),
            new KeyValuePair<string, string>("Input.BaroNo", "12345")
        });

        var response = await client.PostAsync("/Identity/Account/Register", form);

        // Beklenti: redirect (302). LocalRedirect("~/") = "/" yönlendirmesi.
        // 200 dönerse validation hatası vardır (debug için body'yi göster).
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var failBody = await response.Content.ReadAsStringAsync();
            throw new Exception("Beklenen 302 yerine 200 geldi (form yeniden render). Body: " +
                failBody.Substring(0, Math.Min(failBody.Length, 1500)));
        }
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();
        user!.FullName.Should().Be("Register Test");
        user.IsActive.Should().BeTrue();
        user.NotificationsEmailEnabled.Should().BeTrue();
        user.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));

        // Rol kontrolü
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = await userManager.GetRolesAsync(user);
        roles.Should().Contain("Kullanici");

        // UserProfile kontrolü
        var profile = await ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Register Test");
        profile.MeslekTuru.Should().Be(MeslekTuru.Avukat);
        profile.BaroNo.Should().Be("12345");
        profile.MeslekTuruDiger.Should().BeNull();

        await RemoveUserIfExistsAsync(email);
    }

    [Fact]
    public async Task Post_Register_DuplicateEmail_ReturnsValidationError()
    {
        const string email = "duplicate@example.com";
        await RemoveUserIfExistsAsync(email);

        // Önce direct DB'ye bir kullanıcı seed et
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existing = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Existing",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(existing, "ValidPass123!");
            result.Succeeded.Should().BeTrue($"seed user creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var client = CreateClient(allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Register");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.Email", email),
            new KeyValuePair<string, string>("Input.FullName", "Second Try"),
            new KeyValuePair<string, string>("Input.Password", "AnotherPass123!"),
            new KeyValuePair<string, string>("Input.ConfirmPassword", "AnotherPass123!")
        });

        var response = await client.PostAsync("/Identity/Account/Register", form);

        // Page() döner — 200 + validation summary'de hata
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Identity duplicate email mesajı: "is already taken" (varsayılan kültür İngilizce)
        body.Should().Contain("already taken");

        await RemoveUserIfExistsAsync(email);
    }
}
