using System.Net;
using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Tests.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LexCalculus.Tests.Profil;

[Collection("AdminWebHost")]
public class UyeBaglantilarPageTests : IClassFixture<SqlServerTestAuthWebApplicationFactory>
{
    private readonly SqlServerTestAuthWebApplicationFactory _factory;

    public UyeBaglantilarPageTests(SqlServerTestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAnonClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private async Task<(ApplicationUser user, UserProfile profile)> SeedAsync(
        string email, string displayName, string slug,
        bool isPublic, bool showConnections, bool isActive = true)
    {
        await CleanupAsync(email, slug);
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = displayName,
            CreatedAt = DateTime.UtcNow, IsActive = isActive, EmailConfirmed = true
        };
        var r = await um.CreateAsync(user, "ValidPass123!");
        r.Succeeded.Should().BeTrue();

        var profile = new UserProfile
        {
            UserId = user.Id, DisplayName = displayName, PublicSlug = slug,
            IsPublicProfile = isPublic, ShowConnections = showConnections
        };
        ctx.UserProfiles.Add(profile);
        await ctx.SaveChangesAsync();
        return (user, profile);
    }

    private async Task CleanupAsync(string email, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var byEmail = await ctx.Users.FirstOrDefaultAsync(u => u.Email == email);
        var bySlug = await ctx.UserProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.PublicSlug == slug);
        if (bySlug is not null) ctx.UserProfiles.Remove(bySlug);
        if (byEmail is not null)
        {
            var conns = await ctx.UserConnections
                .Where(c => c.RequesterId == byEmail.Id || c.TargetId == byEmail.Id)
                .ToListAsync();
            ctx.UserConnections.RemoveRange(conns);

            var blocks = await ctx.UserBlocks
                .Where(b => b.BlockerId == byEmail.Id || b.BlockedId == byEmail.Id)
                .ToListAsync();
            ctx.UserBlocks.RemoveRange(blocks);

            var profile = await ctx.UserProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == byEmail.Id);
            if (profile is not null) ctx.UserProfiles.Remove(profile);
            ctx.Users.Remove(byEmail);
        }
        await ctx.SaveChangesAsync();
    }

    private async Task SeedAcceptedConnectionAsync(int userA, int userB)
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.UserConnections.Add(new UserConnection
        {
            RequesterId = userA, TargetId = userB,
            Status = UserConnectionStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task OnGet_SlugMissing_ReturnsNotFound()
    {
        using var client = CreateAnonClient();
        var response = await client.GetAsync("/uye/var-olmayan-slug-xyz/baglantilar");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnGet_InactiveUser_ReturnsNotFound()
    {
        var slug = "uyb-inactive-test";
        var (user, _) = await SeedAsync("uyb-inactive@example.com", "Inactive",
            slug, isPublic: true, showConnections: true, isActive: false);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PrivateProfile_ShowsHiddenProfileMessage()
    {
        var slug = "uyb-private-test";
        var (user, _) = await SeedAsync("uyb-private@example.com", "Gizli Üye",
            slug, isPublic: false, showConnections: true);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Bu profil gizli");
            body.Should().NotContain("kullanici-kart-list");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PublicButShowConnectionsFalse_ShowsHiddenListMessage()
    {
        var slug = "uyb-public-noshow";
        var (user, _) = await SeedAsync("uyb-public-noshow@example.com", "Gizli Liste",
            slug, isPublic: true, showConnections: false);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("bağlantı listesini gizli");
            body.Should().NotContain("kullanici-kart-list");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PublicAndShowConnectionsTrue_ListsConnections()
    {
        var slug = "uyb-listed-owner";
        var otherSlug = "uyb-listed-other";
        var (owner, _) = await SeedAsync("uyb-listed-owner@example.com", "Liste Sahibi",
            slug, isPublic: true, showConnections: true);
        var (other, _) = await SeedAsync("uyb-listed-other@example.com", "Bağlı Avukat",
            otherSlug, isPublic: true, showConnections: false);
        try
        {
            await SeedAcceptedConnectionAsync(owner.Id, other.Id);

            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("kullanici-kart-list");
            body.Should().Contain("Ba"); // "Bağlı" — Türkçe karakter encode olur, ASCII parça
            body.Should().Contain($"/uye/{otherSlug}");
        }
        finally
        {
            await CleanupAsync(other.Email!, otherSlug);
            await CleanupAsync(owner.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PublicAndShowConnectionsTrue_NoConnections_ShowsEmptyMessage()
    {
        var slug = "uyb-empty-list";
        var (user, _) = await SeedAsync("uyb-empty@example.com", "Bos Liste",
            slug, isPublic: true, showConnections: true);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Hen"); // "Henüz bir bağlantı yok" — encode-safe parça
            body.Should().NotContain("kullanici-kart-list");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }

    [Fact]
    public async Task OnGet_PublicAndShowConnectionsTrue_HasProfileBackLink()
    {
        var slug = "uyb-back-link";
        var (user, _) = await SeedAsync("uyb-back@example.com", "Geri Dön",
            slug, isPublic: true, showConnections: true);
        try
        {
            using var client = CreateAnonClient();
            var response = await client.GetAsync($"/uye/{slug}/baglantilar");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain($"href=\"/uye/{slug}\"",
                "Profile dön bağlantısı render edilmeli");
        }
        finally
        {
            await CleanupAsync(user.Email!, slug);
        }
    }
}
