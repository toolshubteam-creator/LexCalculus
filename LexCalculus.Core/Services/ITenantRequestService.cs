using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Services;

public sealed record TenantRequestDto(
    int Id,
    string ProposedName,
    string? ProposedSlug,
    string BarSicilNo,
    TenantRequestStatus Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? RejectionReason,
    int? CreatedTenantId);

public sealed record TenantRequestListItemDto(
    int Id,
    int RequestedByUserId,
    string RequestedByUserName,
    string ProposedName,
    string BarSicilNo,
    TenantRequestStatus Status,
    DateTime CreatedAt);

public sealed record TenantRequestDetailDto(
    int Id,
    int RequestedByUserId,
    string RequestedByUserName,
    string RequestedByEmail,
    string ProposedName,
    string? ProposedSlug,
    string BarSicilNo,
    string? Description,
    TenantRequestStatus Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? ProcessedByUserName,
    string? RejectionReason,
    int? CreatedTenantId);

public sealed record CreateTenantRequestInput(
    string ProposedName,
    string? ProposedSlug,
    string BarSicilNo,
    string? Description);

public sealed record ApproveTenantRequestInput(
    string FinalName,
    string? FinalSlug);

public interface ITenantRequestService
{
    // User
    Task<TenantRequestDto?> GetActiveRequestForUserAsync(int userId, CancellationToken ct = default);
    Task<List<TenantRequestDto>> GetUserRequestHistoryAsync(int userId, CancellationToken ct = default);
    Task<int> CreateRequestAsync(int userId, CreateTenantRequestInput input, CancellationToken ct = default);
    Task CancelRequestAsync(int requestId, int userId, CancellationToken ct = default);

    // Admin
    Task<List<TenantRequestListItemDto>> GetAllAsync(TenantRequestStatus? statusFilter, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<TenantRequestDetailDto?> GetByIdAsync(int requestId, CancellationToken ct = default);
    Task ApproveAsync(int requestId, int adminUserId, ApproveTenantRequestInput input, CancellationToken ct = default);
    Task RejectAsync(int requestId, int adminUserId, string rejectionReason, CancellationToken ct = default);
}
