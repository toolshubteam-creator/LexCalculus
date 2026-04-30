using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class PublicProfileService : IPublicProfileService
{
    private const int MaxSuffixAttempts = 1000;

    private readonly ApplicationDbContext _ctx;

    public PublicProfileService(ApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<string> GenerateUniquePublicSlugAsync(
        string? baseInput,
        int? excludeUserId,
        CancellationToken ct = default)
    {
        var seed = SlugHelper.Generate(baseInput);
        if (string.IsNullOrWhiteSpace(seed))
        {
            // Fallback: kullanıcı id'si yoksa "uye" ile başla; Id varsa "uye-{id}"
            seed = excludeUserId.HasValue ? $"uye-{excludeUserId.Value}" : "uye";
        }
        if (seed.Length > 100) seed = seed[..100];

        if (!await IsSlugTakenAsync(seed, excludeUserId, ct))
            return seed;

        for (var i = 2; i < MaxSuffixAttempts; i++)
        {
            var suffix = $"-{i}";
            var maxBaseLen = 100 - suffix.Length;
            var candidate = (seed.Length > maxBaseLen ? seed[..maxBaseLen] : seed) + suffix;
            if (!await IsSlugTakenAsync(candidate, excludeUserId, ct))
                return candidate;
        }

        // Astronomik düşük ihtimal — yine de defansif
        throw new InvalidOperationException(
            $"Slug üretilemedi: {MaxSuffixAttempts} suffix denendi, hepsi alındı.");
    }

    public Task<bool> IsSlugTakenAsync(string slug, int? excludeUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return Task.FromResult(false);

        var q = _ctx.UserProfiles.IgnoreQueryFilters()
            .Where(p => p.PublicSlug == slug);
        if (excludeUserId.HasValue)
            q = q.Where(p => p.UserId != excludeUserId.Value);

        return q.AnyAsync(ct);
    }

    public async Task<UserProfile> EnsureProfileExistsAsync(
        int userId,
        string displayName,
        CancellationToken ct = default)
    {
        var profile = await _ctx.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is not null)
        {
            // Var olan profile — slug eksikse tamamla (eski kayıtlar için
            // geri uyumluluk; yeni kullanıcılarda zaten dolu olur).
            if (string.IsNullOrEmpty(profile.PublicSlug))
            {
                profile.PublicSlug = await GenerateUniquePublicSlugAsync(
                    string.IsNullOrWhiteSpace(profile.DisplayName) ? displayName : profile.DisplayName,
                    userId, ct);
                await _ctx.SaveChangesAsync(ct);
            }
            return profile;
        }

        // Profile yok — yeni oluştur, slug otomatik üret.
        var newDisplayName = string.IsNullOrWhiteSpace(displayName) ? $"uye-{userId}" : displayName.Trim();
        var slug = await GenerateUniquePublicSlugAsync(newDisplayName, userId, ct);

        profile = new UserProfile
        {
            UserId = userId,
            DisplayName = newDisplayName,
            PublicSlug = slug,
            IsPublicProfile = false,
            ShowTenant = false
        };
        _ctx.UserProfiles.Add(profile);
        await _ctx.SaveChangesAsync(ct);
        return profile;
    }
}
