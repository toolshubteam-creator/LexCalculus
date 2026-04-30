using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities;

/// <summary>
/// Sistem genelinde gerçekleşen denetim olayı. Kim, ne zaman, hangi entity
/// üstünde, hangi action'ı tetikledi. Faz 3.8 P1/2'de eklenir.
///
/// Identity'den ayrı bir concern — bu yüzden Identity klasörü dışında.
/// UserName denormalize: kullanıcı silinince FK SetNull olur ama log kalır.
/// </summary>
public class ActivityLog
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = default!;

    [StringLength(100)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public string? MetadataJson { get; set; }

    public int? TenantId { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public ApplicationUser? User { get; set; }
    public Tenant? Tenant { get; set; }
}
