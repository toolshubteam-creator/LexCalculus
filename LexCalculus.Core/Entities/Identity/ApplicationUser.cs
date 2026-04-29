using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace LexCalculus.Core.Entities.Identity;

/// <summary>
/// Application user — extends ASP.NET Identity user with int primary key
/// and domain-specific fields.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>
    /// User's full display name (separate from UserName).
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// UTC timestamp when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last successful login. Updated by sign-in handler.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// If false, the user is suspended and cannot log in.
    /// Used for account suspension by admins (different from email confirmation).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// One-to-one navigation to the user's profile (legal practitioner details).
    /// Nullable because profile is created lazily after registration.
    /// </summary>
    public UserProfile? Profile { get; set; }

    /// <summary>
    /// Master switch for outbound email notifications. Default true (opt-in).
    /// Faz 3.6'da kullanıcı ayarları sayfası bunu UI'da toggle eder.
    /// Stale parameter digest, system alerts gibi e-postalar bunu kontrol eder.
    /// In-app bildirimler (Notification entity) bu flag'den ETKİLENMEZ —
    /// kullanıcı bell icon'da yine görür, sadece e-posta engellenir.
    /// </summary>
    public bool NotificationsEmailEnabled { get; set; } = true;

    /// <summary>
    /// Kullanıcının mesleği (opsiyonel). null = belirtilmemiş.
    /// Yetki belirlemez — istatistik ve hedefli bildirim için.
    /// </summary>
    public MeslekTuru? MeslekTuru { get; set; }

    /// <summary>
    /// MeslekTuru = Diger ise serbest metin açıklama (örn. "Hukuk Müşaviri").
    /// MeslekTuru başka değer ise null olmalı.
    /// </summary>
    [MaxLength(50)]
    public string? MeslekTuruDiger { get; set; }

    /// <summary>Avukat/Hakim/Savci/Bilirkisi gibi sicil/baro numarası (opsiyonel).</summary>
    [MaxLength(30)]
    public string? BaroSicilNo { get; set; }

    /// <summary>Telefon numarası (opsiyonel).</summary>
    [MaxLength(20)]
    public string? Telefon { get; set; }
}
