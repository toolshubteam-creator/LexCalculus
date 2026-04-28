using LexCalculus.Core.Entities.Calculators;

namespace LexCalculus.Core.Services;

/// <summary>
/// Admin paneli için LifeTable yönetimi. Mevcut ILifeTableService
/// calculator-okuma için (cache'li, hızlı); bu interface admin CRUD için.
/// </summary>
public interface ILifeTableAdminService
{
    /// <summary>Tüm tablolar (aktif + pasif), aktif önce, EffectiveDate desc.</summary>
    Task<IReadOnlyList<LifeTable>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Detay görünüm — Rows include edilmiş.</summary>
    Task<LifeTable?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Atomik aktivasyon: eski aktif tabloyu pasif yapar, verilen id'yi
    /// aktif yapar. Tek transaction içinde. Cache invalidation tetiklenir.
    /// Eğer id zaten aktifse no-op.
    /// </summary>
    Task ActivateAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Şu anki aktif tabloyu pasif yapar (hiç aktif kalmamış olur).
    /// Kullanım nadirdir; genellikle aktivasyon ile başka tabloya geçilir.
    /// </summary>
    Task DeactivateActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Yeni LifeTable + 200 satır oluşturur. Yeni tablo PASİF olarak eklenir
    /// (aktivasyon ayrı bir kullanıcı kararı). Code unique constraint:
    /// duplicate ise InvalidOperationException fırlatır.
    /// </summary>
    Task<int> CreateAsync(
        string code,
        string name,
        DateTime effectiveDate,
        string? source,
        string? note,
        IReadOnlyList<LifeTableRow> rows,
        CancellationToken ct = default);
}
