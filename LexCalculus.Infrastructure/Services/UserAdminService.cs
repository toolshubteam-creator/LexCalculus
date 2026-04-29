using System.Text;
using LexCalculus.Core.Email;
using LexCalculus.Core.Email.Models;
using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexCalculus.Infrastructure.Services;

public sealed class UserAdminService : IUserAdminService
{
    private static readonly string[] ValidRoles = { "Admin", "Editor", "Kullanici" };

    private readonly ApplicationDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _emailRenderer;
    private readonly ILogger<UserAdminService> _logger;

    public UserAdminService(
        ApplicationDbContext ctx,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IEmailTemplateRenderer emailRenderer,
        ILogger<UserAdminService> logger)
    {
        _ctx = ctx;
        _userManager = userManager;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _logger = logger;
    }

    public async Task<UserListPage> GetUsersAsync(
        int page, int pageSize,
        string? roleFilter = null,
        bool? isActiveFilter = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 25;

        var q = _userManager.Users.AsQueryable();

        if (isActiveFilter.HasValue)
            q = q.Where(u => u.IsActive == isActiveFilter.Value);

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            q = from u in q
                where _ctx.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_ctx.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .Contains(roleFilter)
                select u;
        }

        var totalCount = await q.CountAsync(ct);
        var users = await q
            .Include(u => u.Profile)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var roleAssignments = await (
            from ur in _ctx.UserRoles
            join r in _ctx.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }
        ).ToListAsync(ct);
        var roleMap = roleAssignments
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).FirstOrDefault());

        var items = users.Select(u => new UserListItem(
            Id: u.Id,
            Email: u.Email ?? "",
            FullName: u.FullName,
            RoleName: roleMap.TryGetValue(u.Id, out var role) ? role : null,
            IsActive: u.IsActive,
            CreatedAt: u.CreatedAt,
            LastLoginAt: u.LastLoginAt,
            MeslekTuruLabel: BuildMeslekLabel(u.Profile))).ToList();

        return new UserListPage(items, totalCount, page, pageSize);
    }

    public async Task<UserDetailViewModel?> GetUserDetailAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userManager.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault();

        var calculations = await _ctx.CalculationHistories
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(30)
            .Select(c => new UserCalculationItem(c.Id, c.ToolSlug, c.CreatedAt))
            .ToListAsync(ct);

        return new UserDetailViewModel(
            Id: user.Id,
            Email: user.Email ?? "",
            FullName: user.FullName,
            RoleName: roleName,
            IsActive: user.IsActive,
            EmailConfirmed: user.EmailConfirmed,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            MeslekTuruLabel: BuildMeslekLabel(user.Profile),
            BaroNo: user.Profile?.BaroNo,
            PhoneNumber: user.PhoneNumber,
            RecentCalculations: calculations);
    }

    public async Task<bool> SetActiveAsync(int userId, bool active, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        user.IsActive = active;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return false;

        // Aktif session'ı geçersiz kıl (cookie next request'te reddedilir)
        await _userManager.UpdateSecurityStampAsync(user);
        return true;
    }

    public async Task<bool> ChangeRoleAsync(int userId, string newRoleName, CancellationToken ct = default)
    {
        if (!ValidRoles.Contains(newRoleName)) return false;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return false;
        }

        var addResult = await _userManager.AddToRoleAsync(user, newRoleName);
        if (!addResult.Succeeded) return false;

        await _userManager.UpdateSecurityStampAsync(user);
        return true;
    }

    public async Task<bool> SendPasswordResetEmailAsync(int userId, string siteUrl, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || string.IsNullOrWhiteSpace(user.Email)) return false;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = $"{siteUrl}/Identity/Account/ResetPassword?code={encoded}&email={Uri.EscapeDataString(user.Email)}";

        var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;

        var model = new PasswordResetModel
        {
            DisplayName = displayName,
            ResetUrl = resetUrl,
            SiteUrl = siteUrl
        };

        try
        {
            var html = await _emailRenderer.RenderAsync("PasswordReset", model, ct);
            var message = new EmailMessage(
                ToAddress: user.Email,
                ToDisplayName: displayName,
                Subject: "Lex Calculus — Şifre Sıfırlama",
                HtmlBody: html);

            return await _emailService.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre reset mail gönderilemedi: {Email}", user.Email);
            return false;
        }
    }

    private static string? BuildMeslekLabel(UserProfile? profile)
    {
        if (profile?.MeslekTuru is null) return null;
        return profile.MeslekTuru switch
        {
            Core.Enums.MeslekTuru.Avukat => "Avukat",
            Core.Enums.MeslekTuru.Hakim => "Hâkim",
            Core.Enums.MeslekTuru.Savci => "Savcı",
            Core.Enums.MeslekTuru.Bilirkisi => "Bilirkişi",
            Core.Enums.MeslekTuru.MaliMusavir => "Mali Müşavir",
            Core.Enums.MeslekTuru.Diger => string.IsNullOrWhiteSpace(profile.MeslekTuruDiger)
                ? "Diğer"
                : $"Diğer: {profile.MeslekTuruDiger}",
            _ => null
        };
    }
}
