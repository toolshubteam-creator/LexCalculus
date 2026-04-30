using FluentAssertions;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Services;
using LexCalculus.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LexCalculus.Tests.Profil;

/// <summary>
/// Faz 4.1 P2-fix — Yaklaşım 4 (görünmez slug). EnsureProfileExistsAsync
/// kayıt anında çağrılır (Identity Register + DavetController.Kayit).
/// </summary>
public class EnsureProfileExistsTests
{
    [Fact]
    public async Task EnsureProfileExists_CreatesProfileWithSlugFromDisplayName()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(new ApplicationUser
        {
            Id = 1,
            UserName = "u1@x.com",
            NormalizedUserName = "U1@X.COM",
            Email = "u1@x.com",
            NormalizedEmail = "U1@X.COM",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);

        var profile = await svc.EnsureProfileExistsAsync(1, "Mesut Gür");

        profile.Should().NotBeNull();
        profile.UserId.Should().Be(1);
        profile.PublicSlug.Should().Be("mesut-gur",
            "SlugHelper Türkçe karakter normalize + lowercase + tire ile temizler");
        profile.IsPublicProfile.Should().BeFalse(
            "default — kullanıcı UI'da açabilir");
        profile.ShowTenant.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureProfileExists_AppendsSuffixOnSlugConflict()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.AddRange(
            MakeUser(1, "u1@x.com"),
            MakeUser(2, "u2@x.com"));
        // İlk kullanıcının profili zaten "mesut-gur" slug'lı
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 1,
            DisplayName = "Mesut Gür",
            PublicSlug = "mesut-gur"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile2 = await svc.EnsureProfileExistsAsync(2, "Mesut Gür");

        profile2.PublicSlug.Should().Be("mesut-gur-2",
            "GenerateUniquePublicSlugAsync çakışmada -2/-3/... suffix ekler");
    }

    [Fact]
    public async Task EnsureProfileExists_FallsBackToUyeIdWhenDisplayNameEmpty()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(99, "anon@x.com"));
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile = await svc.EnsureProfileExistsAsync(99, "");

        profile.PublicSlug.Should().Be("uye-99",
            "DisplayName boşsa fallback uye-{userId}");
    }

    [Fact]
    public async Task EnsureProfileExists_IsIdempotent_DoesNothingWhenProfileWithSlugExists()
    {
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(7, "u7@x.com"));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 7,
            DisplayName = "Mevcut",
            PublicSlug = "mevcut-slug",
            IsPublicProfile = true,
            Bio = "Var olan bio"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);

        // Aynı çağrıyı birden fazla kez yap
        var p1 = await svc.EnsureProfileExistsAsync(7, "Yeni Display");
        var p2 = await svc.EnsureProfileExistsAsync(7, "Başka Yeni");

        p1.PublicSlug.Should().Be("mevcut-slug",
            "mevcut slug korunur, ikinci çağrıda da yeni üretilmez");
        p2.PublicSlug.Should().Be("mevcut-slug");
        p2.IsPublicProfile.Should().BeTrue("mevcut alanlar dokunulmaz");
        p2.Bio.Should().Be("Var olan bio");

        var count = await ctx.UserProfiles.CountAsync(x => x.UserId == 7);
        count.Should().Be(1, "duplicate satır oluşturulmaz");
    }

    [Fact]
    public async Task EnsureProfileExists_FillsSlugIfProfileExistsWithNullSlug()
    {
        // Geri uyumluluk: Faz 4.1 P1/3 öncesi oluşturulmuş profilenin
        // PublicSlug null olabilir; EnsureProfileExistsAsync slug doldurur,
        // diğer alanlara dokunmaz.
        await using var ctx = TestDbContextFactory.Create();
        ctx.Users.Add(MakeUser(11, "legacy@x.com"));
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = 11,
            DisplayName = "Eski Kayıt",
            PublicSlug = null,
            Bio = "Eski bio"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile = await svc.EnsureProfileExistsAsync(11, "Eski Kayıt");

        profile.PublicSlug.Should().Be("eski-kayit",
            "null slug DisplayName'den (entity üstü) üretilir");
        profile.Bio.Should().Be("Eski bio", "diğer alanlar korunur");
    }

    private static ApplicationUser MakeUser(int id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
