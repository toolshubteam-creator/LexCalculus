namespace LexCalculus.Core.Services;

/// <summary>
/// Logs completed calculations for logged-in users.
/// Anonymous users (UserId null or non-positive) silently skipped.
///
/// Phase 2: write-only.
/// Phase 3: read methods will be added (GetUserHistoryAsync, etc.)
/// </summary>
public interface ICalculationHistoryService
{
    /// <summary>
    /// Persist a completed calculation. Failure is non-fatal — logging
    /// MUST NOT break the calculation flow.
    /// </summary>
    Task LogAsync<TInput, TResult>(
        int? userId,
        string categorySlug,
        string toolSlug,
        string toolTitle,
        TInput input,
        TResult result,
        decimal? totalAmount,
        string? unit,
        CancellationToken cancellationToken = default);
}
