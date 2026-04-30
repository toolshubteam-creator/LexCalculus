using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Services;

/// <summary>
/// Public profil yönetimi (Faz 4 — sosyal platform). Şu an slug üretimi +
/// profile guarantee; P2/3'te avatar yükleme MediaUploadService'e taşındı,
/// P3/3'te /uye/{slug} render bağlanacak.
/// </summary>
public interface IPublicProfileService
{
    /// <summary>
    /// Verilen baseInput'tan SlugHelper ile slug üretir, DB'de unique olduğunu
    /// garantiler. Çakışma varsa "-2", "-3", ... suffix ekleyerek dener.
    /// excludeUserId verilirse o kullanıcının kendi slug'ı uniqueness kontrolünden
    /// dışlanır (kendi profilini düzenliyor — kendi slug'ı ile çakışma sayılmasın).
    /// baseInput boş/sluglaştırılamaz ise kullanıcı Id'si tabanlı fallback üretilir.
    /// </summary>
    Task<string> GenerateUniquePublicSlugAsync(
        string? baseInput,
        int? excludeUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Verilen slug'ın başka bir kullanıcı tarafından kullanılıp kullanılmadığı.
    /// excludeUserId verilirse o kullanıcının mevcut slug'ı false sayılır.
    /// </summary>
    Task<bool> IsSlugTakenAsync(string slug, int? excludeUserId, CancellationToken ct = default);

    /// <summary>
    /// Idempotent — kullanıcının UserProfile satırı yoksa oluşturur, varsa
    /// sadece PublicSlug null ise otomatik üretir. Identity Register ve
    /// DavetController.Kayit kayıt anında çağırır. Faz 4.1 P2-fix tasarım kararı
    /// (Yaklaşım 4): slug görünmez kimlik, kullanıcı UI'da görmez/değiştirmez.
    /// </summary>
    /// <returns>Profile (slug'ı garantili dolu).</returns>
    Task<UserProfile> EnsureProfileExistsAsync(
        int userId,
        string displayName,
        CancellationToken ct = default);
}
