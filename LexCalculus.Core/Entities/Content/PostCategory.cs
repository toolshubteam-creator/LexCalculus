namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Makale kategorisi (master). Admin tarafından yönetilir; kullanıcılar
/// makale yazarken seçer. Hard delete YOK — devre dışı bırakılır
/// (mevcut makaleler kategori referansını korur).
/// Faz 4.5 — charter §3.3.
/// </summary>
public sealed class PostCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
