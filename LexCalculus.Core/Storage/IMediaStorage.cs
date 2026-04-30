namespace LexCalculus.Core.Storage;

/// <summary>
/// Media depolama abstraksiyonu. Faz 4'te yerel disk implementasyonu, Faz 5'te
/// Azure Blob (charter §2.2 Karar 13). Tüm path'ler WebRoot-relative
/// (forward-slash), GetPublicUrl ile absolute URL'e çevrilir.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// İçeriği subdirectory altına yazar. Subdirectory yoksa oluşturulur.
    /// Mevcut aynı isimli dosya OVERWRITE edilir (caller GUID kullanmalı).
    /// </summary>
    /// <returns>WebRoot-relative path, örn. "uploads/avatars/2/abc.webp".</returns>
    Task<string> StoreAsync(
        Stream content,
        string subdirectory,
        string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Verilen relative path'teki dosyayı siler. Dosya yoksa sessiz no-op (idempotent).
    /// IOException durumunda log + swallow (caller'ı bozmaz).
    /// </summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Relative path için tarayıcının erişebileceği URL üretir. Production'da
    /// SeoSettings.SiteUrl, dev'de "/" prefix.
    /// </summary>
    string GetPublicUrl(string relativePath);
}
