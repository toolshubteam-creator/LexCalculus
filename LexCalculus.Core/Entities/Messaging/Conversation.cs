using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Messaging;

/// <summary>
/// İki kullanıcı arasındaki 1-1 mesajlaşma kanalı. User1Id &lt; User2Id
/// deterministic konvensiyonu ile tek conversation garantisi
/// (composite unique index). LastMessageAt sıralama + son aktivite,
/// User1LastReadAt/User2LastReadAt okundu durumu için.
/// Faz 5.4, charter Karar 1, 3, 4, 5.
/// </summary>
public sealed class Conversation
{
    public int Id { get; set; }

    /// <summary>Deterministic order: User1Id &lt; User2Id (servis seviyesinde Math.Min).</summary>
    public int User1Id { get; set; }
    public ApplicationUser User1 { get; set; } = null!;

    public int User2Id { get; set; }
    public ApplicationUser User2 { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    /// <summary>Son mesaj zamanı — listeleme + son aktivite. Mesaj silindiğinde geri set edilmez.</summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>User1'in son okuma zamanı; null ise hiç okumadı.</summary>
    public DateTime? User1LastReadAt { get; set; }

    /// <summary>User2'nin son okuma zamanı.</summary>
    public DateTime? User2LastReadAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
