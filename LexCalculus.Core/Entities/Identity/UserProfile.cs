using LexCalculus.Core.Entities.Common;

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
    /// Professional title — Avukat, Stajyer Avukat, Hâkim, Bilirkişi, Akademisyen, etc.
    /// Free-text for now; may become enum in future.
    /// </summary>
    public string? Title { get; set; }

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
    /// Navigation property — the ApplicationUser this profile belongs to.
    /// </summary>
    public ApplicationUser? User { get; set; }
}
