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

public sealed class TenantInvitationService : ITenantInvitationService
{
    private static readonly TimeSpan ExpirationWindow = TimeSpan.FromDays(7);

    private readonly ApplicationDbContext _ctx;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _emailRenderer;
    private readonly ILogger<TenantInvitationService> _logger;
    private readonly string _siteUrl;

    public TenantInvitationService(
        ApplicationDbContext ctx,
        IEmailService emailService,
        IEmailTemplateRenderer emailRenderer,
        IOptions<SeoSettings> seoSettings,
        ILogger<TenantInvitationService> logger)
    {
        _ctx = ctx;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _siteUrl = seoSettings.Value.SiteUrl;
        _logger = logger;
    }

    public async Task<int> CreateAsync(
        int tenantId, int invitedByUserId, string email, bool requesterIsAdmin,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("E-posta zorunlu.");

        email = email.Trim().ToLowerInvariant();

        var tenant = await _ctx.Tenants
            .AsAdminQuery()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct)
            ?? throw new InvalidOperationException("Tenant bulunamadı veya silinmiş.");

        var inviter = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == invitedByUserId, ct)
            ?? throw new InvalidOperationException("Davet eden kullanıcı bulunamadı.");

        if (!requesterIsAdmin && tenant.OwnerUserId != invitedByUserId)
            throw new UnauthorizedAccessException("Bu tenant'a davet etme yetkiniz yok.");

        if (!string.IsNullOrWhiteSpace(inviter.Email)
            && string.Equals(inviter.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kendinizi davet edemezsiniz.");

        // E-posta enumeration koruması: zaten herhangi bir tenant'a üye olan email'e
        // generic mesaj. (Tabii davet eden için aşağıdaki kontrol başarısızsa fail.)
        var existingMember = await _ctx.Users
            .AsAdminQuery()
            .Where(u => u.Email != null && u.Email.ToLower() == email && u.TenantId.HasValue)
            .AnyAsync(ct);
        if (existingMember)
            throw new InvalidOperationException("Bu e-posta davet edilemiyor.");

        // Aynı tenant + email Pending davet varsa eski Cancelled, yeni Pending.
        var existingPending = await _ctx.TenantInvitations
            .AsAdminQuery()
            .Where(i => i.TenantId == tenantId
                        && i.Email == email
                        && i.Status == TenantInvitationStatus.Pending)
            .ToListAsync(ct);
        foreach (var old in existingPending)
            old.Status = TenantInvitationStatus.Cancelled;

        var token = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var invitation = new TenantInvitation
        {
            TenantId = tenantId,
            Email = email,
            Token = token,
            InvitedByUserId = invitedByUserId,
            Status = TenantInvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + ExpirationWindow
        };
        _ctx.TenantInvitations.Add(invitation);
        await _ctx.SaveChangesAsync(ct);

        await SendInvitationEmailAsync(tenant, inviter, invitation, ct);

        return invitation.Id;
    }

    public async Task CancelAsync(int invitationId, int requestedByUserId, bool isAdmin, CancellationToken ct = default)
    {
        var invitation = await _ctx.TenantInvitations
            .AsAdminQuery()
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct)
            ?? throw new InvalidOperationException("Davet bulunamadı.");

        if (invitation.Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Yalnızca bekleyen davetler iptal edilebilir.");

        if (!isAdmin && invitation.Tenant != null && invitation.Tenant.OwnerUserId != requestedByUserId)
            throw new UnauthorizedAccessException("Bu daveti iptal etme yetkiniz yok.");

        invitation.Status = TenantInvitationStatus.Cancelled;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<InvitationLookupResult> LookupByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new InvitationLookupResult { IsValid = false, InvalidReason = "notfound" };

        var invitation = await _ctx.TenantInvitations
            .AsAdminQuery()
            .Include(i => i.Tenant)
            .Include(i => i.InvitedBy)
            .FirstOrDefaultAsync(i => i.Token == token, ct);

        if (invitation == null)
            return new InvitationLookupResult { IsValid = false, InvalidReason = "notfound" };

        if (invitation.Status == TenantInvitationStatus.Cancelled)
            return new InvitationLookupResult { IsValid = false, InvalidReason = "cancelled" };
        if (invitation.Status == TenantInvitationStatus.Accepted)
            return new InvitationLookupResult { IsValid = false, InvalidReason = "accepted" };

        if (invitation.Status == TenantInvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = TenantInvitationStatus.Expired;
            await _ctx.SaveChangesAsync(ct);
            return new InvitationLookupResult { IsValid = false, InvalidReason = "expired" };
        }

        if (invitation.Status == TenantInvitationStatus.Expired)
            return new InvitationLookupResult { IsValid = false, InvalidReason = "expired" };

        return new InvitationLookupResult
        {
            IsValid = true,
            InvitationId = invitation.Id,
            TenantId = invitation.TenantId,
            TenantName = invitation.Tenant?.Name,
            Email = invitation.Email,
            InvitedByUserName = invitation.InvitedBy?.Email ?? invitation.InvitedBy?.UserName,
            ExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task AcceptAsync(string token, int userId, CancellationToken ct = default)
    {
        var lookup = await LookupByTokenAsync(token, ct);
        if (!lookup.IsValid)
            throw new InvalidOperationException($"Davet geçersiz: {lookup.InvalidReason}");

        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        if (!string.Equals(user.Email, lookup.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bu davet farklı bir e-posta için.");

        if (user.TenantId.HasValue)
            throw new InvalidOperationException("Zaten bir tenant'a bağlısınız.");

        await ApplyAcceptanceAsync(lookup.InvitationId!.Value, userId, ct);
    }

    public async Task AcceptForNewUserAsync(string token, int newUserId, CancellationToken ct = default)
    {
        var lookup = await LookupByTokenAsync(token, ct);
        if (!lookup.IsValid)
            throw new InvalidOperationException($"Davet geçersiz: {lookup.InvalidReason}");

        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstOrDefaultAsync(u => u.Id == newUserId, ct)
            ?? throw new InvalidOperationException("Yeni kullanıcı bulunamadı.");

        if (user.TenantId.HasValue)
            throw new InvalidOperationException("Yeni kullanıcı zaten bir tenant'a bağlı.");

        if (!string.Equals(user.Email, lookup.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Email davet ile eşleşmiyor.");

        await ApplyAcceptanceAsync(lookup.InvitationId!.Value, newUserId, ct);
    }

    private async Task ApplyAcceptanceAsync(int invitationId, int userId, CancellationToken ct)
    {
        var invitation = await _ctx.TenantInvitations
            .AsAdminQuery()
            .FirstAsync(i => i.Id == invitationId, ct);

        // Race koruma: status hâlâ Pending mi?
        if (invitation.Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Davet artık geçerli değil.");

        var user = await _ctx.Users
            .AsAdminQuery()
            .FirstAsync(u => u.Id == userId, ct);

        user.TenantId = invitation.TenantId;
        invitation.Status = TenantInvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<List<InvitationListItemDto>> GetForTenantAsync(int tenantId, CancellationToken ct = default)
    {
        var rows = await _ctx.TenantInvitations
            .AsAdminQuery()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Email,
                i.Status,
                i.CreatedAt,
                i.ExpiresAt,
                InvitedByName = i.InvitedBy != null ? i.InvitedBy.Email ?? i.InvitedBy.UserName : null
            })
            .ToListAsync(ct);

        return rows.Select(r => new InvitationListItemDto
        {
            Id = r.Id,
            Email = r.Email,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ExpiresAt = r.ExpiresAt,
            InvitedByUserName = r.InvitedByName ?? "(silinmiş)"
        }).ToList();
    }

    private async Task SendInvitationEmailAsync(Tenant tenant, ApplicationUser inviter, TenantInvitation invitation, CancellationToken ct)
    {
        try
        {
            var inviterName = !string.IsNullOrWhiteSpace(inviter.FullName)
                ? inviter.FullName
                : (inviter.Email ?? inviter.UserName ?? "Lex Calculus üyesi");
            var acceptUrl = $"{_siteUrl.TrimEnd('/')}/davet/{invitation.Token}";

            var model = new TenantInvitationEmailModel
            {
                TenantName = tenant.Name,
                InvitedByUserName = inviterName,
                Email = invitation.Email,
                AcceptUrl = acceptUrl,
                ExpiresAt = invitation.ExpiresAt,
                SiteUrl = _siteUrl
            };

            var html = await _emailRenderer.RenderAsync("TenantInvitation", model, ct);
            await _emailService.SendAsync(new EmailMessage(
                ToAddress: invitation.Email,
                ToDisplayName: null,
                Subject: $"Lex Calculus — {tenant.Name} davetiyesi",
                HtmlBody: html), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant invitation email failed for {Email}", invitation.Email);
        }
    }
}
