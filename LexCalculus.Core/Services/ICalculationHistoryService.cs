using LexCalculus.Core.Entities;

namespace LexCalculus.Core.Services;

/// <summary>
/// Logs completed calculations for logged-in users and exposes read paths.
/// Anonymous users (UserId null or non-positive) silently skipped on write.
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
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user's calculation history with pagination + optional filters.
    /// Sorted by CreatedAt DESC.
    /// scope: "all" (default) | "mine" | "shared-by-me" | "team"
    /// — see CalculationHistoryService implementation for exact predicate.
    /// </summary>
    Task<CalculationHistoryPage> GetForUserAsync(
        int userId,
        int page,
        int pageSize,
        string? toolSlugFilter = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        string? scope = null,
        CancellationToken ct = default);

    /// <summary>
    /// UserId → UserName eşlemesi (paylaşılan hesaplarda "kim paylaştı" göstermek için).
    /// Soft-delete edilen kullanıcılar dahil — UserName istisna durumlar dışında stable.
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> GetUserNamesAsync(
        IReadOnlyCollection<int> userIds, CancellationToken ct = default);

    /// <summary>
    /// Tek bir hesabın paylaşım durumunu togglelar.
    /// share=true ise CalculationHistory.TenantId = user.TenantId (varsa);
    /// share=false ise null. Owner kontrolü yapılır (UserId == requestedByUserId).
    /// </summary>
    Task<bool> SetSharingAsync(
        int historyId, int requestedByUserId, bool share, CancellationToken ct = default);

    /// <summary>
    /// Returns a single history entry. Verifies ownership: returns null if
    /// the entry doesn't belong to the requesting user. Admins use a
    /// separate method (GetByIdForAdminAsync, Parça 3/3'te).
    /// </summary>
    Task<CalculationHistory?> GetByIdForUserAsync(
        int historyId, int userId, CancellationToken ct = default);

    /// <summary>
    /// Distinct ToolSlug list for the user — used for filter dropdown.
    /// </summary>
    Task<IReadOnlyList<string>> GetUsedToolSlugsForUserAsync(
        int userId, CancellationToken ct = default);

    /// <summary>
    /// Admin görünümü: tüm kullanıcıların hesaplarını sayfalama + filtre ile döner.
    /// userIdFilter null = tüm kullanıcılar.
    /// </summary>
    Task<CalculationHistoryPage> GetAllPaginatedAsync(
        int page,
        int pageSize,
        string? toolSlugFilter = null,
        int? userIdFilter = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// Hesap kaydı yapmış distinct kullanıcı ID'leri (admin filtre dropdown için).
    /// </summary>
    Task<IReadOnlyList<int>> GetUsersWithHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Admin görünümü: ownership kontrolü YAPMAZ. Tek bir history kaydını döner
    /// (admin başkasının hesabını görebilir). Bu metot SADECE Admin policy'si
    /// olan controller'lardan çağrılmalı.
    /// </summary>
    Task<CalculationHistory?> GetByIdForAdminAsync(int historyId, CancellationToken ct = default);
}

public sealed record CalculationHistoryPage(
    IReadOnlyList<CalculationHistory> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
