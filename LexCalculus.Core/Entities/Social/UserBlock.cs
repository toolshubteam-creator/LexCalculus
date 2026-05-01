using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Social;

/// <summary>
/// Bir kullanıcının başka bir kullanıcıyı engellediği kayıt. Engelleme tek
/// yönlü tutulur (BlockerId → BlockedId) — engelleyen sessizce, engellenen
/// fark etmez. Charter §2.1 Karar 5, §3.3.
/// </summary>
public sealed class UserBlock
{
    public int Id { get; set; }

    /// <summary>Engelleyen kullanıcı.</summary>
    public int BlockerId { get; set; }
    public ApplicationUser Blocker { get; set; } = null!;

    /// <summary>Engellenen kullanıcı.</summary>
    public int BlockedId { get; set; }
    public ApplicationUser Blocked { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
