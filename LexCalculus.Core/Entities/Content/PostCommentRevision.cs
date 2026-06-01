namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Bir yorumun İLK düzenlemesinde saklanan orijinal hâli (Faz 6.8, tech-debt
/// #21). Yorum başına en fazla bir revision tutulur — sonraki düzenlemeler bu
/// kaydı DEĞİŞTİRMEZ, yani her zaman ilk gönderilen orijinali yansıtır.
/// Yorum silinince cascade ile silinir (KVKK).
/// </summary>
public sealed class PostCommentRevision
{
    public int Id { get; set; }

    public int CommentId { get; set; }
    public PostComment Comment { get; set; } = null!;

    /// <summary>İlk düzenleme öncesi yorumun sanitize edilmiş HTML gövdesi.</summary>
    public string OriginalBody { get; set; } = null!;

    /// <summary>Yorumun ilk oluşturulma tarihi (PostComment.CreatedAt kopyası).</summary>
    public DateTime OriginalCreatedAt { get; set; }

    /// <summary>İlk düzenlemenin yapıldığı an.</summary>
    public DateTime FirstEditedAt { get; set; }
}
