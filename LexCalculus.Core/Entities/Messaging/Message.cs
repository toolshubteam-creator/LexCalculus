using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Messaging;

/// <summary>
/// Conversation içinde tek mesaj. Body sanitize edilmiş HTML
/// (CommentBodyProcessor pattern reuse — text + &lt;br&gt; + auto-link &lt;a&gt;).
/// Soft delete: IsDeleted=true, Body korunur DB'de; UI '(silindi)' placeholder.
/// Faz 5.4 (entity), Faz 5.7 (admin moderasyon).
/// </summary>
public sealed class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public int SenderId { get; set; }
    public ApplicationUser Sender { get; set; } = null!;

    /// <summary>Sanitize edilmiş HTML; raw 1000 char limit, sanitize sonrası 2000.</summary>
    public string Body { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    /// <summary>Sahip soft delete (Faz 5.4). Body korunur, view'da placeholder render.</summary>
    public bool IsDeleted { get; set; }
}
