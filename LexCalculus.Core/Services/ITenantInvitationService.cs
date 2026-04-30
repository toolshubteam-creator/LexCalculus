using LexCalculus.Core.Entities.Identity;

namespace LexCalculus.Core.Services;

public sealed class InvitationLookupResult
{
    public bool IsValid { get; set; }
    /// <summary>"notfound", "cancelled", "accepted", "expired" — IsValid=false durumunda.</summary>
    public string? InvalidReason { get; set; }
    public int? InvitationId { get; set; }
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? Email { get; set; }
    public string? InvitedByUserName { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class InvitationListItemDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public TenantInvitationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string InvitedByUserName { get; set; } = "";
    public bool IsExpired => Status == TenantInvitationStatus.Pending && ExpiresAt < DateTime.UtcNow;
}

public interface ITenantInvitationService
{
    /// <summary>
    /// Tenant owner veya global admin yeni davet oluşturur.
    /// requesterIsAdmin=false ise invitedByUserId tenant.OwnerUserId olmalı.
    /// </summary>
    Task<int> CreateAsync(int tenantId, int invitedByUserId, string email, bool requesterIsAdmin, CancellationToken ct = default);

    /// <summary>
    /// Pending daveti iptal eder. Owner kendi tenant'ında veya admin tüm tenant'larda.
    /// </summary>
    Task CancelAsync(int invitationId, int requestedByUserId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Token doğrula — geçerli ise tenant + invitedBy bilgisi, değilse reason.
    /// Anonim erişim güvenli (sadece public bilgi döner).
    /// </summary>
    Task<InvitationLookupResult> LookupByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Mevcut kayıtlı kullanıcı daveti kabul eder.
    /// Kullanıcının email'i davet email'i ile eşleşmeli (case-insensitive).
    /// </summary>
    Task AcceptAsync(string token, int userId, CancellationToken ct = default);

    /// <summary>
    /// Yeni kayıt sonrası: controller Identity ile kullanıcı oluşturduktan sonra
    /// bu metot davet kabul ve TenantId set eder. Email match service tarafında doğrulanır.
    /// </summary>
    Task AcceptForNewUserAsync(string token, int newUserId, CancellationToken ct = default);

    /// <summary>Tenant detay sayfası — tüm geçmiş davetler.</summary>
    Task<List<InvitationListItemDto>> GetForTenantAsync(int tenantId, CancellationToken ct = default);
}
