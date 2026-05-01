namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// UserPost ↔ PostTag arasındaki M2M ilişkisi. Implicit join entity
/// kullanmak yerine ayrı entity — CreatedAt için ileride tag analitik
/// faydalı (hangi tag ne zaman eklendi). Faz 4.6 P1.
/// </summary>
public sealed class PostTagLink
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public UserPost Post { get; set; } = null!;

    public int TagId { get; set; }
    public PostTag Tag { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
