#nullable enable

using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /uye/{slug}/baglantilar — public bağlantı listesi sayfası. Anonim erişilebilir.
/// Görünüm matrisi:
///   profile null → 404
///   user inactive → 404
///   IsPublicProfile=false → "Bu profil gizli" mesajı (404 değil)
///   IsPublicProfile=true && ShowConnections=false → "Liste gizli" mesajı
///   IsPublicProfile=true && ShowConnections=true → liste render
/// Faz 4.2 P3b/3, charter §3.2 Karar 5 (sayı public ama liste opt-in).
/// </summary>
[AllowAnonymous]
public sealed class UyeBaglantilarModel : PageModel
{
    private readonly ApplicationDbContext _ctx;
    private readonly IConnectionService _connections;
    private readonly IMediaStorage _storage;

    public UyeBaglantilarModel(
        ApplicationDbContext ctx,
        IConnectionService connections,
        IMediaStorage storage)
    {
        _ctx = ctx;
        _connections = connections;
        _storage = storage;
    }

    public string Slug { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string? AvatarUrl { get; private set; }
    public bool IsPublicProfile { get; private set; }
    public bool ShowConnections { get; private set; }

    /// <summary>
    /// Profil sahibinin UserId'si — view'da "diğer taraf" hesaplamak için.
    /// </summary>
    public int ProfileUserId { get; private set; }

    public IReadOnlyList<UserConnection> Connections { get; private set; }
        = Array.Empty<UserConnection>();

    /// <summary>
    /// View helper — diğer kullanıcının avatar public URL'i veya null.
    /// </summary>
    public string? GetAvatarUrl(LexCalculus.Core.Entities.Identity.ApplicationUser other)
        => string.IsNullOrEmpty(other.Profile?.AvatarUrl)
            ? null
            : _storage.GetPublicUrl(other.Profile.AvatarUrl);

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var profile = await _ctx.UserProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PublicSlug == slug, ct);

        if (profile is null || profile.User is null) return NotFound();
        if (!profile.User.IsActive) return NotFound();

        Slug = slug;
        ProfileUserId = profile.UserId;
        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? (profile.User.UserName ?? "Kullanıcı")
            : profile.DisplayName;
        AvatarUrl = string.IsNullOrEmpty(profile.AvatarUrl)
            ? null
            : _storage.GetPublicUrl(profile.AvatarUrl);
        IsPublicProfile = profile.IsPublicProfile;
        ShowConnections = profile.ShowConnections;

        // Liste sadece her iki toggle açıkken yüklenir — diğer durumlarda
        // view sınırlı mesaj render eder.
        if (IsPublicProfile && ShowConnections)
        {
            Connections = await _connections.GetActiveForUserAsync(profile.UserId, ct);
        }

        ViewData["Title"] = $"{DisplayName} — Bağlantılar";
        ViewData["NoIndex"] = !IsPublicProfile || !ShowConnections;

        return Page();
    }
}
