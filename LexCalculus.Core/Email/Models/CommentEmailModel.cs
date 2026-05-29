namespace LexCalculus.Core.Email.Models;

/// <summary>
/// Kendi makalesine yorum geldiğinde gönderilen bildirim e-posta modeli
/// (#22, sosyal bildirim). CommentBodyPreview servis tarafında ~200 karaktere
/// kırpılmış olarak gelir.
/// </summary>
public sealed class CommentEmailModel
{
    public required string RecipientDisplayName { get; init; }
    public required string CommenterDisplayName { get; init; }
    public required string PostTitle { get; init; }
    public required string CommentBodyPreview { get; init; }
    public required string PostUrl { get; init; }
}
