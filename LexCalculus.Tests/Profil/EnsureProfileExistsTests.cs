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
public class EnsureProfileExistsTests : SqlServerTestBase
{
    [Fact]
    public async Task EnsureProfileExists_CreatesProfileWithSlugFromDisplayName()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("u1@x.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);

        var profile = await svc.EnsureProfileExistsAsync(user.Id, "Mesut Gür");

        profile.Should().NotBeNull();
        profile.UserId.Should().Be(user.Id);
        profile.PublicSlug.Should().Be("mesut-gur",
            "SlugHelper Türkçe karakter normalize + lowercase + tire ile temizler");
        profile.IsPublicProfile.Should().BeFalse(
            "default — kullanıcı UI'da açabilir");
        profile.ShowTenant.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureProfileExists_AppendsSuffixOnSlugConflict()
    {
        await using var ctx = _db.Create();
        var user1 = MakeUser("u1@x.com");
        var user2 = MakeUser("u2@x.com");
        ctx.Users.AddRange(user1, user2);
        await ctx.SaveChangesAsync();

        // İlk kullanıcının profili zaten "mesut-gur" slug'lı
        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user1.Id,
            DisplayName = "Mesut Gür",
            PublicSlug = "mesut-gur"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile2 = await svc.EnsureProfileExistsAsync(user2.Id, "Mesut Gür");

        profile2.PublicSlug.Should().Be("mesut-gur-2",
            "GenerateUniquePublicSlugAsync çakışmada -2/-3/... suffix ekler");
    }

    [Fact]
    public async Task EnsureProfileExists_FallsBackToUyeIdWhenDisplayNameEmpty()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("anon@x.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile = await svc.EnsureProfileExistsAsync(user.Id, "");

        profile.PublicSlug.Should().Be($"uye-{user.Id}",
            "DisplayName boşsa fallback uye-{userId}");
    }

    [Fact]
    public async Task EnsureProfileExists_IsIdempotent_DoesNothingWhenProfileWithSlugExists()
    {
        await using var ctx = _db.Create();
        var user = MakeUser("u7@x.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            DisplayName = "Mevcut",
            PublicSlug = "mevcut-slug",
            IsPublicProfile = true,
            Bio = "Var olan bio"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);

        // Aynı çağrıyı birden fazla kez yap
        var p1 = await svc.EnsureProfileExistsAsync(user.Id, "Yeni Display");
        var p2 = await svc.EnsureProfileExistsAsync(user.Id, "Başka Yeni");

        p1.PublicSlug.Should().Be("mevcut-slug",
            "mevcut slug korunur, ikinci çağrıda da yeni üretilmez");
        p2.PublicSlug.Should().Be("mevcut-slug");
        p2.IsPublicProfile.Should().BeTrue("mevcut alanlar dokunulmaz");
        p2.Bio.Should().Be("Var olan bio");

        var count = await ctx.UserProfiles.CountAsync(x => x.UserId == user.Id);
        count.Should().Be(1, "duplicate satır oluşturulmaz");
    }

    [Fact]
    public async Task EnsureProfileExists_FillsSlugIfProfileExistsWithNullSlug()
    {
        // Geri uyumluluk: Faz 4.1 P1/3 öncesi oluşturulmuş profilenin
        // PublicSlug null olabilir; EnsureProfileExistsAsync slug doldurur,
        // diğer alanlara dokunmaz.
        await using var ctx = _db.Create();
        var user = MakeUser("legacy@x.com");
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            DisplayName = "Eski Kayıt",
            PublicSlug = null,
            Bio = "Eski bio"
        });
        await ctx.SaveChangesAsync();

        var svc = new PublicProfileService(ctx);
        var profile = await svc.EnsureProfileExistsAsync(user.Id, "Eski Kayıt");

        profile.PublicSlug.Should().Be("eski-kayit",
            "null slug DisplayName'den (entity üstü) üretilir");
        profile.Bio.Should().Be("Eski bio", "diğer alanlar korunur");
    }

    private static ApplicationUser MakeUser(string email) => new()
    {
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
