using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Models.Seo;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Services;

public sealed class TenantRequestService : ITenantRequestService
{
    private readonly ApplicationDbContext _ctx;
    private readonly ITenantAdminService _tenantAdmin;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _emailRenderer;
    private readonly IActivityLogService _activityLog;
    private readonly ILogger<TenantRequestService> _logger;
    private readonly string _siteUrl;

    public TenantRequestService(
        ApplicationDbContext ctx,
        ITenantAdminService tenantAdmin,
        IEmailService emailService,
        IEmailTemplateRenderer emailRenderer,
        IActivityLogService activityLog,
        IOptions<SeoSettings> seoSettings,
        ILogger<TenantRequestService> logger)
    {
        _ctx = ctx;
        _tenantAdmin = tenantAdmin;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _activityLog = activityLog;
        _siteUrl = seoSettings.Value.SiteUrl;
        _logger = logger;
    }

    private static TenantRequestDto ToDto(TenantRequest r) => new(
        r.Id, r.ProposedName, r.ProposedSlug, r.BarSicilNo,
        r.Status, r.CreatedAt, r.ProcessedAt, r.RejectionReason, r.CreatedTenantId);

    public async Task<TenantRequestDto?> GetActiveRequestForUserAsync(int userId, CancellationToken ct = default)
    {
        var r = await _ctx.TenantRequests
            .AsAdminQuery()
            .Where(x => x.RequestedByUserId == userId && x.Status == TenantRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return r is null ? null : ToDto(r);
    }

    public async Task<List<TenantRequestDto>> GetUserRequestHistoryAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _ctx.TenantRequests
            .AsAdminQuery()
            .Where(x => x.RequestedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<int> CreateRequestAsync(int userId, CreateTenantRequestInput input, CancellationToken ct = default)
    {
        var user = await _ctx.Users.AsAdminQuery().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        if (user.TenantId.HasValue)
            throw new InvalidOperationException("Zaten bir tenant'a bağlısınız.");

        var existingPending = await _ctx.TenantRequests
            .AsAdminQuery()
            .AnyAsync(r => r.RequestedByUserId == userId && r.Status == TenantRequestStatus.Pending, ct);
        if (existingPending)
            throw new InvalidOperationException("Bekleyen bir talebiniz var. Önce onu iptal edin veya admin tarafından sonuçlandırılmasını bekleyin.");

        if (string.IsNullOrWhiteSpace(input.ProposedName))
            throw new InvalidOperationException("Tenant adı zorunlu.");
        if (string.IsNullOrWhiteSpace(input.BarSicilNo))
            throw new InvalidOperationException("Baro/Sicil No zorunlu.");

        var entity = new TenantRequest
        {
            RequestedByUserId = userId,
            ProposedName = input.ProposedName.Trim(),
            ProposedSlug = string.IsNullOrWhiteSpace(input.ProposedSlug) ? null : input.ProposedSlug.Trim(),
            BarSicilNo = input.BarSicilNo.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            Status = TenantRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.TenantRequests.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task CancelRequestAsync(int requestId, int userId, CancellationToken ct = default)
    {
        var r = await _ctx.TenantRequests
            .AsAdminQuery()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Talep bulunamadı.");

        if (r.RequestedByUserId != userId)
            throw new InvalidOperationException("Bu talep size ait değil.");

        if (r.Status != TenantRequestStatus.Pending)
            throw new InvalidOperationException("Yalnızca bekleyen talepler iptal edilebilir.");

        r.Status = TenantRequestStatus.Cancelled;
        r.ProcessedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "TenantRequest.Cancel",
            entityType: nameof(TenantRequest),
            entityId: r.Id,
            description: $"Tenant talebi iptal edildi: {r.ProposedName}",
            metadata: new { RequestId = r.Id },
            ct: ct);
    }

    public async Task<List<TenantRequestListItemDto>> GetAllAsync(TenantRequestStatus? statusFilter, CancellationToken ct = default)
    {
        var q = _ctx.TenantRequests.AsAdminQuery().AsQueryable();
        if (statusFilter.HasValue)
            q = q.Where(r => r.Status == statusFilter.Value);

        var rows = await q
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.RequestedByUserId,
                UserName = r.RequestedBy != null ? r.RequestedBy.Email ?? r.RequestedBy.UserName : null,
                r.ProposedName,
                r.BarSicilNo,
                r.Status,
                r.CreatedAt
            })
            .ToListAsync(ct);

        return rows.Select(r => new TenantRequestListItemDto(
            r.Id, r.RequestedByUserId, r.UserName ?? "(silinmiş)",
            r.ProposedName, r.BarSicilNo, r.Status, r.CreatedAt)).ToList();
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => _ctx.TenantRequests
            .AsAdminQuery()
            .CountAsync(r => r.Status == TenantRequestStatus.Pending, ct);

    public async Task<TenantRequestDetailDto?> GetByIdAsync(int requestId, CancellationToken ct = default)
    {
        var r = await _ctx.TenantRequests
            .AsAdminQuery()
            .Include(x => x.RequestedBy)
            .Include(x => x.ProcessedBy)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null) return null;

        return new TenantRequestDetailDto(
            r.Id, r.RequestedByUserId,
            r.RequestedBy?.UserName ?? "",
            r.RequestedBy?.Email ?? "",
            r.ProposedName, r.ProposedSlug, r.BarSicilNo, r.Description,
            r.Status, r.CreatedAt, r.ProcessedAt,
            r.ProcessedBy?.Email ?? r.ProcessedBy?.UserName,
            r.RejectionReason, r.CreatedTenantId);
    }

    public async Task ApproveAsync(int requestId, int adminUserId, ApproveTenantRequestInput input, CancellationToken ct = default)
    {
        var r = await _ctx.TenantRequests
            .AsAdminQuery()
            .FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Talep bulunamadı.");

        if (r.Status != TenantRequestStatus.Pending)
            throw new InvalidOperationException("Talep zaten işlenmiş.");

        // Talep eden kullanıcının TenantId hâlâ null mı (race koruma)
        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == r.RequestedByUserId, ct)
            ?? throw new InvalidOperationException("Talep eden kullanıcı bulunamadı.");

        if (user.TenantId.HasValue)
            throw new InvalidOperationException("Talep eden kullanıcı şu anda başka bir tenant'a bağlı.");

        // TenantAdminService.CreateAsync slug/uniqueness/owner check'lerini yapar.
        var tenantId = await _tenantAdmin.CreateAsync(
            new CreateTenantRequest(input.FinalName, input.FinalSlug, r.RequestedByUserId), ct);

        r.Status = TenantRequestStatus.Approved;
        r.ProcessedAt = DateTime.UtcNow;
        r.ProcessedByUserId = adminUserId;
        r.CreatedTenantId = tenantId;
        await _ctx.SaveChangesAsync(ct);

        var createdTenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstAsync(t => t.Id == tenantId, ct);

        await _activityLog.LogAsync(
            action: "TenantRequest.Approve",
            entityType: nameof(TenantRequest),
            entityId: r.Id,
            description: $"Tenant talebi onaylandı: {r.ProposedName} → '{createdTenant.Name}'",
            metadata: new { RequestId = r.Id, CreatedTenantId = tenantId },
            tenantId: tenantId,
            ct: ct);

        await SendApprovalEmailAsync(user, createdTenant, ct);
    }

    public async Task RejectAsync(int requestId, int adminUserId, string rejectionReason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new InvalidOperationException("Red gerekçesi zorunlu.");

        var r = await _ctx.TenantRequests
            .AsAdminQuery()
            .Include(x => x.RequestedBy)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Talep bulunamadı.");

        if (r.Status != TenantRequestStatus.Pending)
            throw new InvalidOperationException("Talep zaten işlenmiş.");

        r.Status = TenantRequestStatus.Rejected;
        r.ProcessedAt = DateTime.UtcNow;
        r.ProcessedByUserId = adminUserId;
        r.RejectionReason = rejectionReason.Trim();
        await _ctx.SaveChangesAsync(ct);

        await _activityLog.LogAsync(
            action: "TenantRequest.Reject",
            entityType: nameof(TenantRequest),
            entityId: r.Id,
            description: $"Tenant talebi reddedildi: {r.ProposedName}",
            metadata: new { RequestId = r.Id, RejectionReason = r.RejectionReason },
            ct: ct);

        if (r.RequestedBy != null)
            await SendRejectionEmailAsync(r.RequestedBy, r.ProposedName, rejectionReason, ct);
    }

    private async Task SendApprovalEmailAsync(ApplicationUser user, Tenant tenant, CancellationToken ct)
    {
        try
        {
            var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.Email ?? "Kullanıcı");
            var model = new TenantRequestApprovedModel
            {
                DisplayName = displayName,
                TenantName = tenant.Name,
                TenantSlug = tenant.Slug,
                SiteUrl = _siteUrl
            };
            var html = await _emailRenderer.RenderAsync("TenantRequestApproved", model, ct);
            await _emailService.SendAsync(new EmailMessage(
                ToAddress: user.Email ?? "",
                ToDisplayName: displayName,
                Subject: "Lex Calculus — Tenant Talebiniz Onaylandı",
                HtmlBody: html), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant approval email failed for user {UserId}", user.Id);
        }
    }

    private async Task SendRejectionEmailAsync(ApplicationUser user, string proposedName, string reason, CancellationToken ct)
    {
        try
        {
            var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.Email ?? "Kullanıcı");
            var model = new TenantRequestRejectedModel
            {
                DisplayName = displayName,
                ProposedName = proposedName,
                Reason = reason,
                SiteUrl = _siteUrl
            };
            var html = await _emailRenderer.RenderAsync("TenantRequestRejected", model, ct);
            await _emailService.SendAsync(new EmailMessage(
                ToAddress: user.Email ?? "",
                ToDisplayName: displayName,
                Subject: "Lex Calculus — Tenant Talebiniz Reddedildi",
                HtmlBody: html), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant rejection email failed for user {UserId}", user.Id);
        }
    }
}
