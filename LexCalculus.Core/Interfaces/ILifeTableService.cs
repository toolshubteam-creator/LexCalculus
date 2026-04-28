using LexCalculus.Core.Entities.Calculators;
using LexCalculus.Core.Enums;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Lookup service for life tables. Caches results aggressively (tables are
/// effectively immutable). Calculators consume this for actuarial inputs.
/// </summary>
public interface ILifeTableService
{
    /// <summary>
    /// Returns the expected remaining lifetime (eX) for the given age and gender,
    /// using the specified table or the active default if null.
    /// Returns null if no row matches (caller must handle gracefully).
    /// </summary>
    Task<decimal?> GetBekledigiYasamAsync(int yas, Cinsiyet cinsiyet, string? tableCode = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the active life table — the one to use by default when a
    /// calculator doesn't specify a table code. If multiple are Active=true,
    /// returns the one with the latest EffectiveDate.
    /// </summary>
    Task<LifeTable?> GetActiveTableAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all available life tables (active + inactive). For admin UI
    /// and the calculator's table selection dropdown.
    /// </summary>
    Task<IReadOnlyList<LifeTable>> GetAllTablesAsync(CancellationToken ct = default);

    /// <summary>
    /// Cache invalidation. Admin tarafı tablo aktivasyon/satır güncellemesi
    /// sonrası çağırır; calculator sonraki çağrısında DB'den yeni değerleri
    /// okur (cache miss → fresh).
    /// </summary>
    Task InvalidateCacheAsync(CancellationToken ct = default);
}
