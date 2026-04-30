using System.ComponentModel.DataAnnotations;

namespace LexCalculus.Core.Entities.Identity;

public enum TenantInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Cancelled = 2,
    Expired = 3
}

/// <summary>
/// Tenant owner veya global admin tarafından bir e-postaya gönderilen davet.
/// Token URL-safe; alıcı /davet/{token} ile kabul edebilir.
/// Faz 3.7 P3/5.
/// </summary>
public class TenantInvitation
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string Token { get; set; } = default!;

    public int InvitedByUserId { get; set; }
    public TenantInvitationStatus Status { get; set; } = TenantInvitationStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int? AcceptedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public ApplicationUser? InvitedBy { get; set; }
    public ApplicationUser? AcceptedBy { get; set; }
}
