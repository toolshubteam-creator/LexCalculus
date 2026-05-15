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
public class ProfilePageTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public ProfilePageTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthClient(int userId, bool allowAutoRedirect = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect
        });
        client.DefaultRequestHeaders.Add("X-Test-User", $"profile-test-{userId}@local");
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

    private async Task<ApplicationUser> CreateUserWithProfileAsync(
        string email,
        string fullName,
        MeslekTuru? meslekTuru = null,
        string? baroNo = null,
        string? phoneNumber = null)
    {
        await RemoveUserIfExistsAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            PhoneNumber = phoneNumber
        };
        var result = await um.CreateAsync(user, "ValidPass123!");
        result.Succeeded.Should().BeTrue($"seed failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            DisplayName = fullName,
            MeslekTuru = meslekTuru,
            BaroNo = baroNo
        });
        await ctx.SaveChangesAsync();
        return user;
    }

    private async Task<ApplicationUser> CreateUserWithoutProfileAsync(string email, string fullName)
    {
        await RemoveUserIfExistsAsync(email);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };
        var result = await um.CreateAsync(user, "ValidPass123!");
        result.Succeeded.Should().BeTrue();
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
    public async Task Get_Profil_AsAuthenticatedUser_ReturnsFormWithCurrentValues()
    {
        var user = await CreateUserWithProfileAsync(
            "profile-get@example.com", "Profil Test",
            meslekTuru: MeslekTuru.Avukat, baroNo: "12345", phoneNumber: "5551234567");

        var client = CreateAuthClient(user.Id, allowAutoRedirect: true);
        var response = await client.GetAsync("/profil");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Profil Test");        // FullName input value
        body.Should().Contain("12345");              // BaroNo
        body.Should().Contain("5551234567");         // PhoneNumber
        body.Should().Contain("profile-get@example.com"); // Email locked

        await RemoveUserIfExistsAsync("profile-get@example.com");
    }

    [Fact]
    public async Task Get_Profil_LazyCreatesUserProfile()
    {
        var user = await CreateUserWithoutProfileAsync(
            "profile-lazy@example.com", "Lazy User");

        // Henüz UserProfile yok
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await ctx.UserProfiles.AnyAsync(p => p.UserId == user.Id))
                .Should().BeFalse("seed kasıtlı olarak profilsiz");
        }

        var client = CreateAuthClient(user.Id, allowAutoRedirect: true);
        var response = await client.GetAsync("/profil");
        response.IsSuccessStatusCode.Should().BeTrue();

        // Lazy create sonrası profile satırı var
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            profile.Should().NotBeNull();
            profile!.DisplayName.Should().Be("Lazy User");
        }

        await RemoveUserIfExistsAsync("profile-lazy@example.com");
    }

    [Fact]
    public async Task Post_Profil_HappyPath_UpdatesUserAndProfile()
    {
        var user = await CreateUserWithProfileAsync(
            "profile-update@example.com", "Eski Ad",
            meslekTuru: MeslekTuru.Avukat, baroNo: "11111");

        var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/profil");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.FullName", "Yeni Ad"),
            new KeyValuePair<string, string>("Input.MeslekTuru", "2"),    // Hakim
            new KeyValuePair<string, string>("Input.BaroNo", "99999"),
            new KeyValuePair<string, string>("Input.PhoneNumber", "5559876543"),
            new KeyValuePair<string, string>("Input.NotificationsEmailEnabled", "true")
        });

        var response = await client.PostAsync("/profil", form);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var failBody = await response.Content.ReadAsStringAsync();
            throw new Exception("Beklenen 302 yerine 200. Body: " +
                failBody.Substring(0, Math.Min(failBody.Length, 1500)));
        }
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshed = await ctx.Users.FirstAsync(u => u.Id == user.Id);
            refreshed.FullName.Should().Be("Yeni Ad");
            refreshed.PhoneNumber.Should().Be("5559876543");
            refreshed.NotificationsEmailEnabled.Should().BeTrue();

            var profile = await ctx.UserProfiles.FirstAsync(p => p.UserId == user.Id);
            profile.MeslekTuru.Should().Be(MeslekTuru.Hakim);
            profile.BaroNo.Should().Be("99999");
            profile.DisplayName.Should().Be("Yeni Ad");
        }

        await RemoveUserIfExistsAsync("profile-update@example.com");
    }

    [Fact]
    public async Task Post_Profil_DigerWithoutText_ReturnsValidationError()
    {
        var user = await CreateUserWithProfileAsync(
            "profile-diger@example.com", "Diger Test");

        var client = CreateAuthClient(user.Id, allowAutoRedirect: false);
        var token = await GetAntiforgeryTokenAsync(client, "/profil");

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Input.FullName", "Diger Test"),
            new KeyValuePair<string, string>("Input.MeslekTuru", "99"),   // Diger
            new KeyValuePair<string, string>("Input.MeslekTuruDiger", ""),
            new KeyValuePair<string, string>("Input.NotificationsEmailEnabled", "false")
        });

        var response = await client.PostAsync("/profil", form);

        // 200 (Page redisplay) — POST başarılı olsa 302 redirect olurdu
        ((int)response.StatusCode).Should().Be((int)HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Form yeniden render edildi
        body.Should().Contain("Input_MeslekTuruDiger");
        // DB güncellenmediğini doğrula — ana validation kanıtı
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await ctx.UserProfiles.FirstAsync(p => p.UserId == user.Id);
            profile.MeslekTuru.Should().BeNull("validation hatası nedeniyle güncellenmemeli");
            profile.MeslekTuruDiger.Should().BeNull();
        }

        await RemoveUserIfExistsAsync("profile-diger@example.com");
    }
}
