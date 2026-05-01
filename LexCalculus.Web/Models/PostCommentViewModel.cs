namespace LexCalculus.Web.Models;

/// <summary>
/// /uye/{slug}/makale/{slug} sayfasında yorum kart'ları için VM. Yetki
/// (CanEdit, CanDelete) server-side hesaplanır → JS yalnızca render eder.
/// Faz 4.9 P2.
/// </summary>
public sealed class PostCommentViewModel
{
    public int Id { get; set; }
    public int PostId { get; set; }

    /// <summary>Sanitize edilmiş HTML — view'da Html.Raw güvenli.</summary>
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public bool IsEdited { get; set; }

    public string AuthorDisplayName { get; set; } = "";
    public string? AuthorSlug { get; set; }
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>Sahip mi (düzenleme).</summary>
    public bool CanEdit { get; set; }

    /// <summary>Sahip || post sahibi || admin (silme).</summary>
    public bool CanDelete { get; set; }
}
