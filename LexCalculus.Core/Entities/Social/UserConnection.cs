using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Social;

/// <summary>
/// İki kullanıcı arasında LinkedIn-tarzı bağlantı. Tek satır = tek yönlü
/// istek; kabul edildiğinde mantıken karşılıklı (sorgu iki yöne bakar).
/// Faz 4.2 P1/3 — charter §3.2.
/// </summary>
public sealed class UserConnection
{
    public int Id { get; set; }

    public int RequesterId { get; set; }
    public ApplicationUser Requester { get; set; } = null!;

    public int TargetId { get; set; }
    public ApplicationUser Target { get; set; } = null!;

    public UserConnectionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Accept/Reject/Cancel anında set edilir; Pending iken null.</summary>
    public DateTime? RespondedAt { get; set; }
}

public enum UserConnectionStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
    // Removed YOK — Remove hard delete; satır silinir.
}
