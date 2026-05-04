#nullable enable

using System.Text.Encodings.Web;
using System.Text.Json;
using LexCalculus.Core.Common;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Entities.Social;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexCalculus.Web.Pages;

/// <summary>
/// /uye/{slug} — public profile sayfası. Anonim kullanıcılar erişebilir.
/// Charter §2.1 Karar 1 (iki ayrı toggle), §2.4 Karar 22 (gizli profil 404
/// değil, default'lar görünür), §5.3 (KVKK kişisel veri exclusion).
/// </summary>
[AllowAnonymous]
public sealed class UyeModel : PageModel
{
    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;
    private readonly IConnectionService _connections;
    private readonly IUserBlockService _blocks;
    private readonly IConversationService _conversations;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SeoSettings _seo;

    public UyeModel(
        ApplicationDbContext ctx,
        IMediaStorage storage,
        IConnectionService connections,
        IUserBlockService blocks,
        IConversationService conversations,
        UserManager<ApplicationUser> userManager,
        IOptions<SeoSettings> seoOptions)
    {
        _ctx = ctx;
        _storage = storage;
        _connections = connections;
        _blocks = blocks;
        _conversations = conversations;
        _userManager = userManager;
        _seo = seoOptions.Value ?? new SeoSettings();
    }

    public string Slug { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string? AvatarUrl { get; private set; }
    public bool IsPublic { get; private set; }
    public string? Bio { get; private set; }
    public string? City { get; private set; }
    public string? Meslek { get; private set; }
    public string? TenantName { get; private set; }
    public string CanonicalUrl { get; private set; } = "";

    // Faz 4.2 P3a/3 — bağlantı butonu state-aware
    public bool IsViewerAnonymous { get; private set; }
    public bool IsOwnProfile { get; private set; }
    public ConnectionStateResult? ConnectionState { get; private set; }
    public int? ConnectionId { get; private set; }
    public int ConnectionCount { get; private set; }

    // Faz 4.2 P3b/3 — bağlantı listesi görünürlüğü (sayı link mi?)
    public bool ShowConnections { get; private set; }

    // Faz 4.3 — engelleme state'leri
    public bool IsBlockedByMe { get; private set; }
    public bool BlockedByOther { get; private set; }

    // Faz 5.5 — mesajlaşma yetkisi (bağlantı OR aynı tenant + NOT engelleme)
    public bool CanMessage { get; private set; }
    public int ProfileUserId { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var profile = await _ctx.UserProfiles
            .Include(p => p.User)
                .ThenInclude(u => u!.Tenant)
            .FirstOrDefaultAsync(p => p.PublicSlug == slug, ct);

        if (profile is null || profile.User is null) return NotFound();
        if (!profile.User.IsActive) return NotFound();

        // Daima görünür: DisplayName + Avatar (Karar 22 — gizli profilde de placeholder)
        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? (profile.User.UserName ?? "Kullanıcı")
            : profile.DisplayName;
        AvatarUrl = string.IsNullOrEmpty(profile.AvatarUrl)
            ? null
            : _storage.GetPublicUrl(profile.AvatarUrl);
        IsPublic = profile.IsPublicProfile;
        ShowConnections = profile.ShowConnections;

        if (profile.IsPublicProfile)
        {
            Bio = profile.Bio;
            City = profile.City;
            Meslek = profile.MeslekTuru.ToTurkish(profile.MeslekTuruDiger);

            if (profile.ShowTenant && profile.User.TenantId.HasValue && profile.User.Tenant != null)
            {
                TenantName = profile.User.Tenant.Name;
            }
        }

        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');
        Slug = slug;
        CanonicalUrl = $"{siteUrl}/uye/{slug}";

        // Faz 4.2 P3a/3 — viewer perspective
        var viewerIdRaw = _userManager.GetUserId(User);
        IsViewerAnonymous = string.IsNullOrEmpty(viewerIdRaw);
        ProfileUserId = profile.UserId;

        if (!IsViewerAnonymous && int.TryParse(viewerIdRaw, out var viewerId))
        {
            IsOwnProfile = viewerId == profile.UserId;

            if (!IsOwnProfile)
            {
                IsBlockedByMe = await _blocks.IsBlockedAsync(viewerId, profile.UserId, ct);
                BlockedByOther = await _blocks.IsBlockedAsync(profile.UserId, viewerId, ct);

                // Engelleme yoksa connection state + mesaj yetkisi hesapla.
                if (!IsBlockedByMe && !BlockedByOther)
                {
                    ConnectionState = await _connections.GetConnectionStateAsync(viewerId, profile.UserId, ct);

                    if (ConnectionState.State is UserConnectionState.PendingSent
                                              or UserConnectionState.PendingReceived
                                              or UserConnectionState.Accepted)
                    {
                        ConnectionId = await _ctx.UserConnections
                            .Where(c => ((c.RequesterId == viewerId && c.TargetId == profile.UserId)
                                      || (c.RequesterId == profile.UserId && c.TargetId == viewerId))
                                     && (c.Status == UserConnectionStatus.Pending
                                      || c.Status == UserConnectionStatus.Accepted))
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => (int?)c.Id)
                            .FirstOrDefaultAsync(ct);
                    }

                    CanMessage = await _conversations.CanMessageAsync(viewerId, profile.UserId, ct);
                }
            }
        }

        // Bağlantı sayısı sadece IsPublicProfile=true ise (Karar 5)
        if (profile.IsPublicProfile)
        {
            ConnectionCount = await _connections.GetConnectionCountAsync(profile.UserId, ct);
        }

        // ViewData/PageMeta üzerinden SeoMeta + JSON-LD layout'a aktarılır.
        ViewData["Title"] = $"{DisplayName} — Lex Calculus";
        var description = IsPublic && !string.IsNullOrWhiteSpace(Bio)
            ? Truncate(Bio, 160)
            : $"{DisplayName} — Lex Calculus üyesi";

        ViewData["PageMeta"] = new SeoMeta
        {
            Title = $"{DisplayName} — Lex Calculus",
            Description = description,
            CanonicalUrl = CanonicalUrl,
            OgType = "profile",
            OgImage = !string.IsNullOrEmpty(AvatarUrl) && !string.IsNullOrEmpty(siteUrl)
                ? AvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? AvatarUrl
                    : $"{siteUrl}{(AvatarUrl.StartsWith('/') ? "" : "/")}{AvatarUrl}"
                : null,
            JsonLdBlocks = { GetPersonJsonLd() }
        };

        return Page();
    }

    /// <summary>
    /// Schema.org Person JSON-LD üretir. KVKK koruması: BaroNo, e-posta,
    /// telefon hiçbir koşulda dahil edilmez. Test ile dondurulmuş.
    /// </summary>
    public string GetPersonJsonLd()
    {
        var data = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Person",
            ["name"] = DisplayName,
            ["url"] = CanonicalUrl
        };

        if (!string.IsNullOrEmpty(AvatarUrl))
            data["image"] = AvatarUrl;

        if (IsPublic)
        {
            if (!string.IsNullOrEmpty(Bio))
                data["description"] = Bio;

            if (!string.IsNullOrEmpty(Meslek))
                data["jobTitle"] = Meslek;

            if (!string.IsNullOrEmpty(City))
            {
                data["address"] = new Dictionary<string, object>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = City
                };
            }

            if (!string.IsNullOrEmpty(TenantName))
            {
                data["affiliation"] = new Dictionary<string, object>
                {
                    ["@type"] = "Organization",
                    ["name"] = TenantName
                };
            }
        }

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            // Türkçe karakterler escape edilmesin (description okunabilir kalsın)
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        });
    }

    // Faz 4.2 P3a/3 — Bağlantı POST handler'ları (PRG pattern, RedirectToPage(slug))

    [EnableRateLimiting("connection")]
    public async Task<IActionResult> OnPostConnectAsync(string slug, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();

        var targetUserId = await _ctx.UserProfiles
            .Where(p => p.PublicSlug == slug)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
        if (targetUserId is null) return NotFound();

        var result = await _connections.SendAsync(viewerId, targetUserId.Value, ct);
        SetMessage(result, success: "Bağlantı isteği gönderildi.");
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostCancelAsync(string slug, int connectionId, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();
        var result = await _connections.CancelAsync(connectionId, viewerId, ct);
        SetMessage(result, success: "İstek iptal edildi.");
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostAcceptAsync(string slug, int connectionId, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();
        var result = await _connections.AcceptAsync(connectionId, viewerId, ct);
        SetMessage(result, success: "Bağlantı isteği kabul edildi.");
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostRejectAsync(string slug, int connectionId, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();
        var result = await _connections.RejectAsync(connectionId, viewerId, ct);
        SetMessage(result, success: "İstek reddedildi.");
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string slug, int connectionId, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();
        var result = await _connections.RemoveAsync(connectionId, viewerId, ct);
        SetMessage(result, success: "Bağlantı kaldırıldı.");
        return RedirectToPage(new { slug });
    }

    // Faz 4.3 — engelleme handler'ları (cascade Accepted bağlantı silme)

    public async Task<IActionResult> OnPostBlockAsync(string slug, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();

        var targetUserId = await _ctx.UserProfiles
            .Where(p => p.PublicSlug == slug)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
        if (targetUserId is null) return NotFound();

        var result = await _blocks.BlockAsync(viewerId, targetUserId.Value, ct);
        if (result.Success)
            TempData["Success"] = "Kullanıcı engellendi.";
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
        return RedirectToPage(new { slug });
    }

    public async Task<IActionResult> OnPostUnblockAsync(string slug, CancellationToken ct = default)
    {
        if (!TryGetViewerId(out var viewerId)) return Challenge();

        var targetUserId = await _ctx.UserProfiles
            .Where(p => p.PublicSlug == slug)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
        if (targetUserId is null) return NotFound();

        var result = await _blocks.UnblockAsync(viewerId, targetUserId.Value, ct);
        if (result.Success)
            TempData["Success"] = "Engelleme kaldırıldı.";
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
        return RedirectToPage(new { slug });
    }

    private bool TryGetViewerId(out int viewerId)
    {
        var raw = _userManager.GetUserId(User);
        return int.TryParse(raw, out viewerId);
    }

    private void SetMessage(ConnectionResult result, string success)
    {
        if (result.Success)
            TempData["Success"] = success;
        else
            TempData["Error"] = result.ErrorMessage ?? "İşlem başarısız oldu.";
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max];
}
