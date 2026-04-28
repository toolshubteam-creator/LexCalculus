using LexCalculus.Core.Entities.Calculators;

namespace LexCalculus.Core.Interfaces;

/// <summary>
/// Time-versioned parameter lookup for calculator tools.
///
/// Lookup semantics: Given (toolSlug, key, asOfDate), returns the row with the
/// LATEST EffectiveDate that is &lt;= asOfDate. This enables retroactive
/// calculations (e.g. computing severance pay as of a past termination date
/// uses the parameter values that were in force at that date).
///
/// Tool slug "*" is reserved for cross-tool parameters (e.g. minimum wage).
/// Lookup falls back to "*" when the tool-specific row doesn't exist.
///
/// Results are cached in distributed cache (Redis) for 24 hours; invalidate
/// explicitly when admin updates a parameter.
/// </summary>
public interface IFormulaParameterService
{
    /// <summary>
    /// Returns the parameter value as of the given date, or null if no row exists.
    /// Falls back to ToolSlug="*" if the tool-specific row is missing.
    /// </summary>
    Task<decimal?> GetValueAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default);

    /// <summary>
    /// Returns the full parameter row as of the given date (Value + Source + Note + EffectiveDate),
    /// or null if missing. Use when the calculator needs to display "kıdem tavanı (Çalışma Bakanlığı, 2026 ilk yarı)".
    /// </summary>
    Task<FormulaParameter?> GetParameterAsync(string toolSlug, string key, DateTime asOfDate, CancellationToken ct = default);

    /// <summary>
    /// Returns ALL versions of a parameter (history) sorted by EffectiveDate DESC.
    /// Used by admin UI and audit views; not cached (admin operation, infrequent).
    /// </summary>
    Task<IReadOnlyList<FormulaParameter>> GetHistoryAsync(string toolSlug, string key, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new version of a parameter. Throws if a row with the same
    /// (ToolSlug, Key, EffectiveDate) already exists (unique constraint).
    /// Invalidates cache for this (toolSlug, key) pair across all dates.
    /// </summary>
    Task<FormulaParameter> AddAsync(FormulaParameter parameter, CancellationToken ct = default);

    /// <summary>
    /// Manually invalidates cache for a specific (toolSlug, key) pair.
    /// Called by AddAsync; admins may also call after bulk inserts.
    /// </summary>
    Task InvalidateAsync(string toolSlug, string key, CancellationToken ct = default);

    /// <summary>
    /// Returns ALL parameter rows. Soft-deleted rows excluded (global query filter).
    /// Sorted by ToolSlug ASC, Key ASC, EffectiveDate DESC. Used by admin list view.
    /// </summary>
    Task<IReadOnlyList<FormulaParameter>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Mutates an existing parameter row IN PLACE. Use only to fix structural
    /// errors (typo in Key, wrong ToolSlug). DO NOT use to change Value over
    /// time — for that, AddAsync creates a new effective-dated version.
    /// Updates LastModifiedByUserId. Invalidates cache.
    /// </summary>
    Task<FormulaParameter> UpdateAsync(FormulaParameter parameter, int modifiedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes (IsDeleted=true). Cache invalidated. The row stays in DB
    /// for audit purposes but is excluded from all queries (global filter).
    /// Updates LastModifiedByUserId.
    /// </summary>
    Task SoftDeleteAsync(int id, int modifiedByUserId, CancellationToken ct = default);
}
