using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Entities.Common;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Entities.Identity;

/// <summary>
/// Legal practitioner profile — extended user information beyond authentication.
/// One UserProfile per ApplicationUser. Inherits BaseEntity for soft-delete
/// and audit timestamps.
/// </summary>
public class UserProfile : BaseEntity
{
    /// <summary>
    /// Foreign key to ApplicationUser. Unique (one profile per user).
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Public-facing display name. Required, non-null. Defaults to empty string.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Bar association registration number. Optional. Unique across all profiles
    /// when present (filtered unique index in DB layer).
    /// Format and validation are jurisdiction-specific (e.g. Istanbul Bar uses numeric IDs).
    /// </summary>
    public string? BaroNo { get; set; }

    /// <summary>
    /// Kullanıcının mesleği (opsiyonel). null = belirtilmemiş.
    /// Yetki belirlemez — istatistik ve hedefli bildirim için.
    /// Faz 3.6 Parça 2a/4: önceki Title (free-text) yerini aldı.
    /// </summary>
    public MeslekTuru? MeslekTuru { get; set; }

    /// <summary>
    /// MeslekTuru = Diger ise serbest metin açıklama (örn. "Hukuk Müşaviri").
    /// MeslekTuru başka değer ise null olmalı.
    /// </summary>
    [MaxLength(50)]
    public string? MeslekTuruDiger { get; set; }

    /// <summary>
    /// User-provided biography. Plain text, no HTML.
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// URL to the profile image. Stored in MediaFiles table or external CDN.
    /// Null = use default avatar.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Practice location. Free-text Turkish city name.
    /// Used for filtering and SEO (Person schema).
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// If true, profile is visible to anonymous visitors and indexable by search engines
    /// (Person JSON-LD). If false, only logged-in users with a connection can view.
    /// </summary>
    public bool IsPublicProfile { get; set; }

    /// <summary>
    /// URL-safe public slug for /uye/{slug} — only meaningful when IsPublicProfile=true.
    /// Filtered unique index (NULL allowed for users who never opened public profile).
    /// Preserved when IsPublicProfile is toggled off so the user keeps their URL on re-enable.
    /// Faz 4.1 P1/3.
    /// </summary>
    [MaxLength(100)]
    public string? PublicSlug { get; set; }

    /// <summary>
    /// If true and the user has a TenantId, public profile shows the tenant name.
    /// Always treated as false when TenantId is null (defansif: tenant'sız kullanıcılar
    /// için DB'ye true yazılmaz). Faz 4.1 P1/3, charter Karar 1.
    /// </summary>
    public bool ShowTenant { get; set; }

    /// <summary>
    /// Navigation property — the ApplicationUser this profile belongs to.
    /// </summary>
    public ApplicationUser? User { get; set; }
}
