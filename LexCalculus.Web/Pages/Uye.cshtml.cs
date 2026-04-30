#nullable enable

using System.Text.Encodings.Web;
using System.Text.Json;
using LexCalculus.Core.Enums;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
    private readonly SeoSettings _seo;

    public UyeModel(
        ApplicationDbContext ctx,
        IMediaStorage storage,
        IOptions<SeoSettings> seoOptions)
    {
        _ctx = ctx;
        _storage = storage;
        _seo = seoOptions.Value ?? new SeoSettings();
    }

    public string DisplayName { get; private set; } = "";
    public string? AvatarUrl { get; private set; }
    public bool IsPublic { get; private set; }
    public string? Bio { get; private set; }
    public string? City { get; private set; }
    public string? Meslek { get; private set; }
    public string? TenantName { get; private set; }
    public string CanonicalUrl { get; private set; } = "";

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

        if (profile.IsPublicProfile)
        {
            Bio = profile.Bio;
            City = profile.City;
            Meslek = FormatMeslek(profile.MeslekTuru, profile.MeslekTuruDiger);

            if (profile.ShowTenant && profile.User.TenantId.HasValue && profile.User.Tenant != null)
            {
                TenantName = profile.User.Tenant.Name;
            }
        }

        var siteUrl = (_seo.SiteUrl ?? "").TrimEnd('/');
        CanonicalUrl = $"{siteUrl}/uye/{slug}";

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

    private static string? FormatMeslek(MeslekTuru? tur, string? diger)
    {
        if (tur is null) return null;

        if (tur == MeslekTuru.Diger)
            return string.IsNullOrWhiteSpace(diger) ? "Diğer" : diger;

        return tur switch
        {
            MeslekTuru.Avukat => "Avukat",
            MeslekTuru.Hakim => "Hâkim",
            MeslekTuru.Savci => "Savcı",
            MeslekTuru.Bilirkisi => "Bilirkişi",
            MeslekTuru.MaliMusavir => "Mali Müşavir",
            _ => null
        };
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max];
}
