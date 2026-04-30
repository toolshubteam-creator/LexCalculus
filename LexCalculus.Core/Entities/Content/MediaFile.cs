using System.ComponentModel.DataAnnotations;
using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Entities.Content;

/// <summary>
/// Yüklenen medya dosyalarının metadata kaydı (avatar, ileride UGC görselleri).
/// Faz 4.1 P2/3 — charter §3.3, Karar 13/14 (yerel disk başlangıç + ortak altyapı).
///
/// FK Cascade User → MediaFile: kullanıcı silinince medya kayıtları da silinir.
/// (Dosya temizliği IMediaStorage tarafına bırakılmıştır; entity silinmesi DB
/// referansını temizler ama disk üstündeki dosya orphan kalabilir — DataFreshness
/// pattern gibi ayrı bir cleanup job ileride eklenebilir.)
/// </summary>
public class MediaFile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Disk üstünde GUID-tabanlı dosya adı, örn. "abc123def456.webp".</summary>
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = null!;

    /// <summary>Kullanıcının yüklediği orijinal dosya adı (audit için).</summary>
    [Required]
    [StringLength(255)]
    public string OriginalName { get; set; } = null!;

    /// <summary>WebRoot-relative path, örn. "uploads/avatars/2/abc.webp".</summary>
    [Required]
    [StringLength(500)]
    public string RelativePath { get; set; } = null!;

    /// <summary>Çıktı MIME tipi (resize sonrası), örn. "image/webp".</summary>
    [Required]
    [StringLength(100)]
    public string MimeType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}
