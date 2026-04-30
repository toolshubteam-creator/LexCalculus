namespace LexCalculus.Core.Services;

/// <summary>
/// Public profil yönetimi (Faz 4 — sosyal platform). Şu an sadece slug üretimi
/// içeriyor; P2/3'te avatar yükleme, P3/3'te public sayfa render eklenecek.
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
}
