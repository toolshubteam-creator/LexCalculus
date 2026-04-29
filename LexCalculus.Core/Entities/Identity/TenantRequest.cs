using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Entities.Identity;

public enum TenantRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>
/// Kullanıcının "kendi hukuk büromu/baromun tenant'ını açın" başvurusu.
/// Admin onaylarsa Tenant oluşturulur ve owner kullanıcı olur.
/// Faz 3.7 P2b/5.
/// </summary>
public class TenantRequest
{
    public int Id { get; set; }

    public int RequestedByUserId { get; set; }

    [Required]
    [StringLength(200)]
    public string ProposedName { get; set; } = default!;

    /// <summary>Kullanıcının önerdiği slug (opsiyonel, admin onay anında düzenleyebilir).</summary>
    [StringLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$")]
    public string? ProposedSlug { get; set; }

    [Required]
    [StringLength(50)]
    public string BarSicilNo { get; set; } = default!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public TenantRequestStatus Status { get; set; } = TenantRequestStatus.Pending;
    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
    public int? ProcessedByUserId { get; set; }

    [StringLength(1000)]
    public string? RejectionReason { get; set; }

    public int? CreatedTenantId { get; set; }

    public ApplicationUser? RequestedBy { get; set; }
    public ApplicationUser? ProcessedBy { get; set; }
    public Tenant? CreatedTenant { get; set; }
}
