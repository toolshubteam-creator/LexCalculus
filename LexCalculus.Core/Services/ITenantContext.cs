namespace LexCalculus.Core.Services;

/// <summary>
/// HTTP request başına aktif kullanıcı + tenant bağlamı.
/// EF Core global query filter, controller'lar ve servisler bu interface
/// üzerinden mevcut kullanıcı/tenant'ı sorar.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Aktif kullanıcının üye olduğu tenant. Null = bireysel kullanıcı
    /// (varsayılan vatandaş senaryosu).
    /// </summary>
    int? CurrentTenantId { get; }

    /// <summary>
    /// Aktif kullanıcının ApplicationUser.Id değeri. Anonim ise null.
    /// </summary>
    int? CurrentUserId { get; }

    /// <summary>
    /// Kullanıcı bir tenant'a üye mi (CurrentTenantId.HasValue).
    /// </summary>
    bool IsTenantMember { get; }
}
