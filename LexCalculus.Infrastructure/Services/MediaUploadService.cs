using LexCalculus.Core.Entities.Content;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Core.Storage;
using LexCalculus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LexCalculus.Infrastructure.Services;

public sealed class MediaUploadService : IMediaUploadService
{
    private const long MaxBytes = 5 * 1024 * 1024;          // 5 MB
    private const int AvatarSize = 256;                      // 256x256 square
    private const int FeaturedWidth = 1200;                  // OG 1.91:1
    private const int FeaturedHeight = 630;
    private const int InlineMaxDimension = 1200;             // max 1200x1200, aspect korunur
    private const int WebpQuality = 85;

    private readonly ApplicationDbContext _ctx;
    private readonly IMediaStorage _storage;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<MediaUploadService> _logger;

    public MediaUploadService(
        ApplicationDbContext ctx,
        IMediaStorage storage,
        IActivityLogService activityLog,
        ILogger<MediaUploadService> logger)
    {
        _ctx = ctx;
        _storage = storage;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<MediaUploadResult> UploadAvatarAsync(
        int userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default)
    {
        // 1. Boyut + boş kontrol
        if (content == null || sizeBytes <= 0)
            return new MediaUploadResult(false, null, "Dosya seçilmedi.");

        if (sizeBytes > MaxBytes)
            return new MediaUploadResult(false, null,
                $"Dosya çok büyük (max {MaxBytes / 1024 / 1024} MB).");

        // 2. ContentType whitelist (kullanıcı header'ı; magic bytes ile re-validate edilir)
        var declaredCt = (contentType ?? "").ToLowerInvariant();
        if (declaredCt is not ("image/jpeg" or "image/png" or "image/webp"))
            return new MediaUploadResult(false, null,
                "Yalnızca JPG, PNG veya WebP yüklenebilir.");

        // 3. Stream'i belleğe al (ImageSharp + magic bytes için iki kez okumak gerek)
        var ms = new MemoryStream();
        try
        {
            if (content.CanSeek) content.Position = 0;
            await content.CopyToAsync(ms, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Avatar stream okuma hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme okuma hatası.");
        }

        // 4. Magic bytes MIME re-validation
        var detected = DetectImageMime(ms.ToArray());
        if (detected is null)
            return new MediaUploadResult(false, null,
                "Geçersiz görsel dosyası (içerik MIME ile uyuşmuyor).");

        // 5. ImageSharp ile 256x256 square crop + WebP encode + EXIF strip
        ms.Position = 0;
        byte[] webpBytes;
        try
        {
            using var image = await Image.LoadAsync(ms, ct);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(AvatarSize, AvatarSize),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));
            // EXIF strip — ImageSharp default olarak sadece KOPYA edilenleri korur;
            // metadata'yı açıkça temizleyelim (mahremiyet).
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            var encoder = new WebpEncoder { Quality = WebpQuality };
            using var outMs = new MemoryStream();
            await image.SaveAsync(outMs, encoder, ct);
            webpBytes = outMs.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Avatar ImageSharp parse hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null,
                "Görsel işlenemedi (bozuk veya desteklenmeyen format).");
        }
        finally
        {
            await ms.DisposeAsync();
        }

        // 6. Yeni filename + subdirectory + store
        var newFileName = $"{Guid.NewGuid():N}.webp";
        var subdir = $"uploads/avatars/{userId}";
        string relativePath;
        try
        {
            using var writeMs = new MemoryStream(webpBytes);
            relativePath = await _storage.StoreAsync(writeMs, subdir, newFileName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Avatar storage yazma hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme depolama hatası.");
        }

        // 7. Eski avatar varsa sil + UserProfile.AvatarUrl güncelle
        var profile = await _ctx.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile == null)
        {
            // Defansif — Profil sayfası genellikle profile lazy-create ediyor;
            // burada da bir tane oluştur. AvatarUrl set edilebilsin diye gerekli.
            profile = new UserProfile { UserId = userId, DisplayName = "" };
            _ctx.UserProfiles.Add(profile);
        }

        var oldAvatarPath = profile.AvatarUrl;
        profile.AvatarUrl = relativePath;

        // 8. MediaFile audit kaydı
        _ctx.MediaFiles.Add(new MediaFile
        {
            UserId = userId,
            FileName = newFileName,
            OriginalName = TrimToMax(originalFileName ?? newFileName, 255),
            RelativePath = relativePath,
            MimeType = "image/webp",
            SizeBytes = webpBytes.LongLength,
            CreatedAt = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync(ct);

        // 9. Eski dosyayı sil (DB tx başarılı olunca)
        if (!string.IsNullOrWhiteSpace(oldAvatarPath) && oldAvatarPath != relativePath)
        {
            await _storage.DeleteAsync(oldAvatarPath, ct);
            // Eski MediaFile satırını da temizle (dosya sürüleri toplanması için)
            var stale = await _ctx.MediaFiles
                .Where(m => m.UserId == userId && m.RelativePath == oldAvatarPath)
                .ToListAsync(ct);
            if (stale.Count > 0)
            {
                _ctx.MediaFiles.RemoveRange(stale);
                await _ctx.SaveChangesAsync(ct);
            }
        }

        // 10. ActivityLog
        await _activityLog.LogAsync(
            action: "User.AvatarUpload",
            entityType: nameof(UserProfile),
            entityId: profile.Id,
            description: $"Avatar yüklendi: {relativePath}",
            metadata: new { SizeBytes = webpBytes.LongLength, OriginalName = originalFileName },
            ct: ct);

        return new MediaUploadResult(true, relativePath, null);
    }

    public async Task<MediaUploadResult> UploadFeaturedImageAsync(
        int userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (content == null || sizeBytes <= 0)
            return new MediaUploadResult(false, null, "Dosya seçilmedi.");

        if (sizeBytes > MaxBytes)
            return new MediaUploadResult(false, null,
                $"Dosya çok büyük (max {MaxBytes / 1024 / 1024} MB).");

        var declaredCt = (contentType ?? "").ToLowerInvariant();
        if (declaredCt is not ("image/jpeg" or "image/png" or "image/webp"))
            return new MediaUploadResult(false, null,
                "Yalnızca JPG, PNG veya WebP yüklenebilir.");

        var ms = new MemoryStream();
        try
        {
            if (content.CanSeek) content.Position = 0;
            await content.CopyToAsync(ms, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Featured image stream okuma hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme okuma hatası.");
        }

        var detected = DetectImageMime(ms.ToArray());
        if (detected is null)
            return new MediaUploadResult(false, null,
                "Geçersiz görsel dosyası (içerik MIME ile uyuşmuyor).");

        ms.Position = 0;
        byte[] webpBytes;
        try
        {
            using var image = await Image.LoadAsync(ms, ct);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(FeaturedWidth, FeaturedHeight),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            var encoder = new WebpEncoder { Quality = WebpQuality };
            using var outMs = new MemoryStream();
            await image.SaveAsync(outMs, encoder, ct);
            webpBytes = outMs.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Featured image ImageSharp hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null,
                "Görsel işlenemedi (bozuk veya desteklenmeyen format).");
        }
        finally
        {
            await ms.DisposeAsync();
        }

        var newFileName = $"{Guid.NewGuid():N}.webp";
        var subdir = $"uploads/posts/{userId}";
        string relativePath;
        try
        {
            using var writeMs = new MemoryStream(webpBytes);
            relativePath = await _storage.StoreAsync(writeMs, subdir, newFileName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Featured image storage hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme depolama hatası.");
        }

        // MediaFile audit kaydı (eski silme YOK — post bağımsız, üst handler karar verir)
        _ctx.MediaFiles.Add(new MediaFile
        {
            UserId = userId,
            FileName = newFileName,
            OriginalName = TrimToMax(originalFileName ?? newFileName, 255),
            RelativePath = relativePath,
            MimeType = "image/webp",
            SizeBytes = webpBytes.LongLength,
            CreatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "User.FeaturedImageUpload",
            entityType: nameof(MediaFile),
            entityId: null,
            description: $"Makale kapak görseli yüklendi: {relativePath}",
            metadata: new { SizeBytes = webpBytes.LongLength, OriginalName = originalFileName },
            ct: ct);

        return new MediaUploadResult(true, relativePath, null);
    }

    public async Task<MediaUploadResult> UploadInlineImageAsync(
        int userId,
        Stream content,
        string originalFileName,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default)
    {
        if (content == null || sizeBytes <= 0)
            return new MediaUploadResult(false, null, "Dosya seçilmedi.");

        if (sizeBytes > MaxBytes)
            return new MediaUploadResult(false, null,
                $"Dosya çok büyük (max {MaxBytes / 1024 / 1024} MB).");

        var declaredCt = (contentType ?? "").ToLowerInvariant();
        if (declaredCt is not ("image/jpeg" or "image/png" or "image/webp"))
            return new MediaUploadResult(false, null,
                "Yalnızca JPG, PNG veya WebP yüklenebilir.");

        var ms = new MemoryStream();
        try
        {
            if (content.CanSeek) content.Position = 0;
            await content.CopyToAsync(ms, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inline image stream okuma hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme okuma hatası.");
        }

        var detected = DetectImageMime(ms.ToArray());
        if (detected is null)
            return new MediaUploadResult(false, null,
                "Geçersiz görsel dosyası (içerik MIME ile uyuşmuyor).");

        ms.Position = 0;
        byte[] webpBytes;
        try
        {
            using var image = await Image.LoadAsync(ms, ct);
            // Max mode: aspect ratio korunur, en büyük boyut 1200'e indirilir
            // (zaten 1200'den küçükse dokunulmaz).
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(InlineMaxDimension, InlineMaxDimension),
                Mode = ResizeMode.Max
            }));
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            var encoder = new WebpEncoder { Quality = WebpQuality };
            using var outMs = new MemoryStream();
            await image.SaveAsync(outMs, encoder, ct);
            webpBytes = outMs.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inline image ImageSharp hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null,
                "Görsel işlenemedi (bozuk veya desteklenmeyen format).");
        }
        finally
        {
            await ms.DisposeAsync();
        }

        var newFileName = $"{Guid.NewGuid():N}.webp";
        var subdir = $"uploads/posts/{userId}/inline";
        string relativePath;
        try
        {
            using var writeMs = new MemoryStream(webpBytes);
            relativePath = await _storage.StoreAsync(writeMs, subdir, newFileName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inline image storage hatası: user={UserId}", userId);
            return new MediaUploadResult(false, null, "Yükleme depolama hatası.");
        }

        // MediaFile audit kaydı (eski silme YOK — orphan GC Faz 5+)
        _ctx.MediaFiles.Add(new MediaFile
        {
            UserId = userId,
            FileName = newFileName,
            OriginalName = TrimToMax(originalFileName ?? newFileName, 255),
            RelativePath = relativePath,
            MimeType = "image/webp",
            SizeBytes = webpBytes.LongLength,
            CreatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "User.InlineImageUpload",
            entityType: nameof(MediaFile),
            entityId: null,
            description: $"Makale içi görsel yüklendi: {relativePath}",
            metadata: new { SizeBytes = webpBytes.LongLength, OriginalName = originalFileName },
            ct: ct);

        return new MediaUploadResult(true, relativePath, null);
    }

    /// <summary>
    /// İlk 12 byte'dan görsel MIME tespiti. JPEG (FF D8 FF), PNG (89 50 4E 47),
    /// WebP (RIFF....WEBP). Bilinmeyen ise null.
    /// </summary>
    private static string? DetectImageMime(byte[] bytes)
    {
        if (bytes.Length < 4) return null;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        // WebP: 'RIFF' .... 'WEBP'
        if (bytes.Length >= 12 &&
            bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
            bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            return "image/webp";

        return null;
    }

    private static string TrimToMax(string s, int max)
        => s.Length <= max ? s : s[..max];
}
