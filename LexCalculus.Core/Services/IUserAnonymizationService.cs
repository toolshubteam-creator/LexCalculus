namespace LexCalculus.Core.Services;

/// <summary>
/// KVKK 7. madde "unutulma hakkı" — kullanıcı hesabını silme/anonimize.
/// Hard delete YOK; soft anonimize (DB integrity + içerik bağlamı korunur).
/// Charter Faz 5 Karar 6.
///
/// Akış:
/// - Identity user: IsActive=false, e-posta deleted-{id}-{guid}@anonymized.local,
///   şifre null, lockout sonsuz, security stamp invalidate
/// - Profile: DisplayName="Silinmiş Kullanıcı", Bio/City/Avatar/PublicSlug null
/// - UserConnection + UserBlock: hard delete (her iki yön)
/// - UserPost: yayında olanlar otomatik unpublish + tag UsageCount decrement
/// - PostComment, PostLike, ContentReport: KORUNUR (yazar 'Silinmiş Kullanıcı'
///   render edilir GetDisplayNameOrAnonymized helper ile)
/// - Tenant: User bir tenant'ın owner'ıysa ve tenant'ta başka aktif user
///   varsa BLOKE — önce ownership transfer veya tenant silme gerekir
/// </summary>
public interface IUserAnonymizationService
{
    /// <summary>
    /// Pre-check: anonimize yapılabilir mi + bilgilendirme sayıları.
    /// CanProceed=false ise BlockerMessage doludur.
    /// </summary>
    Task<UserAnonymizationCheck> CanAnonymizeAsync(
        int userId, CancellationToken ct = default);

    /// <summary>
    /// Anonimize uygula (atomik). Pre-check otomatik çağrılır; başarısızsa
    /// Result.Success=false döner.
    /// </summary>
    Task<UserAnonymizationResult> AnonymizeAsync(
        int userId, int actingAdminUserId, CancellationToken ct = default);
}

public sealed record UserAnonymizationResult(bool Success, string? ErrorMessage);

public sealed record UserAnonymizationCheck(
    bool CanProceed,
    string? BlockerMessage,
    int ConnectionCount,
    int PostCount,
    int CommentCount);
