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

/// <summary>
/// Admin paneli için kullanıcı yönetimi. Faz 3.6 Parça 1/4 — sadece liste.
/// Detay + actions Parça 4/4'te eklenecek.
/// </summary>
public interface IUserAdminService
{
    Task<UserListPage> GetUsersAsync(
        int page,
        int pageSize,
        string? roleFilter = null,
        bool? isActiveFilter = null,
        CancellationToken ct = default);
}
