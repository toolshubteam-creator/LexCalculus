namespace LexCalculus.Core.Services;

public sealed record TenantListItemDto(
    int Id,
    string Name,
    string Slug,
    string OwnerUserName,
    int MemberCount,
    DateTime CreatedAt,
    bool IsDeleted);

public sealed record UserOptionDto(
    int Id,
    string UserName,
    string Email);

public sealed record TenantDetailDto(
    int Id,
    string Name,
    string Slug,
    int OwnerUserId,
    string OwnerUserName,
    DateTime CreatedAt,
    bool IsDeleted,
    IReadOnlyList<UserOptionDto> Members);

public sealed record CreateTenantRequest(
    string Name,
    string? Slug,
    int OwnerUserId);

public sealed record UpdateTenantRequest(
    string Name,
    string Slug,
    int OwnerUserId);

/// <summary>
/// Admin paneli için Tenant CRUD. Sadece global Admin yetkisiyle erişilir
/// (controller [Authorize] guard'ı). Faz 3.7 P2a/5 — manuel oluşturma;
/// kullanıcı self-service akışı P2b/5 ile gelecek.
/// </summary>
public interface ITenantAdminService
{
    Task<List<TenantListItemDto>> GetAllAsync(
        bool includeDeleted,
        string? search,
        CancellationToken ct = default);

    Task<TenantDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);

    Task UpdateAsync(int id, UpdateTenantRequest request, CancellationToken ct = default);

    Task SoftDeleteAsync(int id, CancellationToken ct = default);

    Task AddMemberAsync(int tenantId, int userId, CancellationToken ct = default);

    Task RemoveMemberAsync(int tenantId, int userId, CancellationToken ct = default);

    Task<List<UserOptionDto>> GetAvailableUsersAsync(CancellationToken ct = default);
}
