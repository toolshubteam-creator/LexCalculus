using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Email;

/// <summary>
/// Dijest e-postası için biriken kalem (Adım 6.2 P2). Şu an yalnızca mesaj
/// dijesti (EmailDigestType.Message): yeni mesaj geldiğinde MessageService bir
/// kayıt ekler (recipient master + EmailOnMessageDigest açıksa). ProcessMessageDigestJob
/// 5 dk pencere dolunca UserId başına toplar, tek e-posta gönderir, IsSent işaretler.
/// </summary>
public sealed class EmailDigestEntry
{
    public int Id { get; set; }

    /// <summary>Dijesti alacak kullanıcı (FK, cascade delete).</summary>
    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public EmailDigestType Type { get; set; }

    /// <summary>İlişkili entity (Message için MessageId). Sender listesi bundan türetilir.</summary>
    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
}

/// <summary>Dijest türü. Explicit int — yeni tür eklenirse mevcut satırlar bozulmaz.</summary>
public enum EmailDigestType
{
    Message = 1
}
