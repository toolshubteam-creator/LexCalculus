using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Entities.Identity;

/// <summary>
/// Multi-tenant unit (hukuk bürosu, baro, vs.). Faz 3.7'de ekleniyor —
/// hukuk ekibi senaryosu için (5 avukatın ortak hesap geçmişi paylaşımı).
/// Bireysel vatandaş kullanıcılar için TenantId nullable; bireysel kullanım
/// pattern'i etkilenmez.
///
/// Vizyon: sistem ücretsiz, Plan kavramı (Free/Pro) YOK.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tenant adı zorunludur.")]
    [StringLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// URL-safe slug — küçük harf, rakam, tire. Unique.
    /// </summary>
    [Required(ErrorMessage = "Slug zorunludur.")]
    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]+$",
        ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir.")]
    public string Slug { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Tenant'ı kuran ve owner yetkisine sahip ApplicationUser.Id (int).
    /// </summary>
    public int OwnerUserId { get; set; }

    /// <summary>Soft-delete flag. Tenant silinince üyeler etkilenmez (TenantId null'a dönmesi için SetNull cascade).</summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ApplicationUser? Owner { get; set; }
    public ICollection<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
}
