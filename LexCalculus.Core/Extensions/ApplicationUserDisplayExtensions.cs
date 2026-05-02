using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Extensions;

/// <summary>
/// ApplicationUser görüntüleme yardımcıları. Anonimize edilmiş (IsActive=false)
/// kullanıcılar tüm UI'larda "Silinmiş Kullanıcı" olarak render edilir
/// (Faz 5 charter Karar 6, KVKK uyum). Tek-kaynak fallback chain'i sağlar:
/// inaktif → AnonymizedDisplayName, aktif → Profile.DisplayName ?? UserName ?? "Kullanıcı".
/// </summary>
public static class ApplicationUserDisplayExtensions
{
    public const string AnonymizedDisplayName = "Silinmiş Kullanıcı";

    /// <summary>
    /// Görüntülenecek isim. Anonimize edilmiş kullanıcı için sabit
    /// "Silinmiş Kullanıcı" döner; aktif kullanıcı için Profile.DisplayName,
    /// yoksa UserName, hiçbiri yoksa "Kullanıcı".
    /// User null ise de Anonymized döner (defansif — silinmiş referans).
    /// </summary>
    public static string GetDisplayNameOrAnonymized(this ApplicationUser? user)
    {
        if (user is null) return AnonymizedDisplayName;
        if (!user.IsActive) return AnonymizedDisplayName;
        if (!string.IsNullOrWhiteSpace(user.Profile?.DisplayName))
            return user.Profile!.DisplayName;
        return string.IsNullOrWhiteSpace(user.UserName) ? "Kullanıcı" : user.UserName!;
    }

    /// <summary>
    /// Anonimize edilmiş veya null. UI'da link/avatar gizlenmesi için kontrol.
    /// </summary>
    public static bool IsAnonymizedOrInactive(this ApplicationUser? user)
        => user is null || !user.IsActive;

    /// <summary>
    /// PublicSlug yalnızca aktif kullanıcı için. Anonimize sonrası slug null
    /// olur ama defansif: inaktif user için her zaman null döner.
    /// </summary>
    public static string? GetPublicSlugOrNull(this ApplicationUser? user)
    {
        if (user is null || !user.IsActive) return null;
        return user.Profile?.PublicSlug;
    }
}
