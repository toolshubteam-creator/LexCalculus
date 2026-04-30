using LexCalculus.Core.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Storage;

/// <summary>
/// IMediaStorage'ın yerel disk implementasyonu — wwwroot altına yazar.
/// Faz 5'te AzureBlobMediaStorage ile değiştirilebilir; mevcut DI tek
/// satır değişikliğiyle migration olur. Charter §2.2 Karar 13.
/// </summary>
public sealed class LocalDiskMediaStorage : IMediaStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalDiskMediaStorage> _logger;

    public LocalDiskMediaStorage(
        IWebHostEnvironment env,
        ILogger<LocalDiskMediaStorage> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> StoreAsync(
        Stream content,
        string subdirectory,
        string fileName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_env.WebRootPath))
            throw new InvalidOperationException("WebRootPath ayarlanmamış (IWebHostEnvironment).");

        var normalizedSubdir = subdirectory.Replace('\\', '/').Trim('/');
        var dirAbsolute = Path.Combine(_env.WebRootPath, normalizedSubdir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dirAbsolute);

        var pathAbsolute = Path.Combine(dirAbsolute, fileName);
        await using var fs = new FileStream(
            pathAbsolute, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        if (content.CanSeek) content.Position = 0;
        await content.CopyToAsync(fs, ct);

        return $"{normalizedSubdir}/{fileName}";
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(_env.WebRootPath)) return Task.CompletedTask;

        var pathAbsolute = Path.Combine(
            _env.WebRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

        try
        {
            if (File.Exists(pathAbsolute))
                File.Delete(pathAbsolute);
        }
        catch (IOException ex)
        {
            // Dosya kilitli olabilir (anti-virüs vb.); orphan dosya log'la, akışı bozma.
            _logger.LogWarning(ex, "Media dosyası silinemedi: {Path}", pathAbsolute);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

        // Faz 4.1 P2/3 fix: her ortamda relative URL döndür — browser
        // current origin'i kullanır, dev (localhost) ve prod (lexcalculus.com)
        // sorunsuz çalışır. JSON-LD/sitemap/og:image gibi absolute URL gerektiren
        // yerler için ayrı bir GetAbsoluteUrl helper'ı (P3/3'te) eklenir.
        var clean = relativePath.Replace('\\', '/').TrimStart('/');
        return "/" + clean;
    }
}
