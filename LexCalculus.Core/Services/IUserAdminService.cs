namespace LexCalculus.Core.Services;

public sealed record UserListItem(
    int Id,
    string Email,
    string? FullName,
    string? RoleName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string? MeslekTuruLabel);

public sealed record UserListPage(
    IReadOnlyList<UserListItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record UserCalculationItem(
    int Id,
    string ToolSlug,
    DateTime CreatedAt);

public sealed record UserDetailViewModel(
    int Id,
    string Email,
    string? FullName,
    string? RoleName,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string? MeslekTuruLabel,
    string? BaroNo,
    string? PhoneNumber,
    IReadOnlyList<UserCalculationItem> RecentCalculations);

/// <summary>
/// Admin paneli için kullanıcı yönetimi. Faz 3.6 Parça 1/4 list,
/// Parça 4/4 detay + actions.
/// </summary>
public interface IUserAdminService
{
    Task<UserListPage> GetUsersAsync(
        int page,
        int pageSize,
        string? roleFilter = null,
        bool? isActiveFilter = null,
        CancellationToken ct = default);

    Task<UserDetailViewModel?> GetUserDetailAsync(int userId, CancellationToken ct = default);

    Task<bool> SetActiveAsync(int userId, bool active, CancellationToken ct = default);

    Task<bool> ChangeRoleAsync(int userId, string newRoleName, CancellationToken ct = default);

    Task<bool> SendPasswordResetEmailAsync(int userId, string siteUrl, CancellationToken ct = default);
}
