namespace LexCalculus.Core.Services;

/// <summary>
/// Yüklenen medya dosyalarını işleme servisi (size limit, MIME re-validation,
/// resize/format, depolama, eski dosya temizliği). Faz 4.1 P2/3 — şu an
/// sadece avatar; Dalga B'de UploadPostImageAsync eklenecek.
///
/// Core katmanı ASP.NET'e bağımlı olmasın diye IFormFile yerine primitif
/// parametre kullanılır. Web/Razor handler IFormFile.OpenReadStream() ile
/// stream'i geçer.
/// </summary>
public interface IMediaUploadService
{
    Task<MediaUploadResult> UploadAvatarAsync(
        int userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default);

    /// <summary>
    /// Makale kapak görseli yükleme. 1200x630 (Open Graph 1.91:1) crop, WebP,
    /// EXIF strip, max 5 MB. Avatar'dan farklı: post per-image (eski silinmez,
    /// post update kararı üst handler'a).
    /// </summary>
    Task<MediaUploadResult> UploadFeaturedImageAsync(
        int userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default);
}

public sealed record MediaUploadResult(
    bool Success,
    string? RelativePath,
    string? ErrorMessage);
