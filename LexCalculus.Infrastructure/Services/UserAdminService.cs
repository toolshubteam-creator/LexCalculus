using LexCalculus.Core.Entities.Identity;
using LexCalculus.Core.Services;
using LexCalculus.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Services;

public sealed class UserAdminService : IUserAdminService
{
    private readonly ApplicationDbContext _ctx;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserAdminService(ApplicationDbContext ctx, UserManager<ApplicationUser> userManager)
    {
        _ctx = ctx;
        _userManager = userManager;
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

        var items = users.Select(u =>
        {
            string? meslekLabel = u.MeslekTuru switch
            {
                Core.Enums.MeslekTuru.Avukat => "Avukat",
                Core.Enums.MeslekTuru.Hakim => "Hâkim",
                Core.Enums.MeslekTuru.Savci => "Savcı",
                Core.Enums.MeslekTuru.Bilirkisi => "Bilirkişi",
                Core.Enums.MeslekTuru.MaliMusavir => "Mali Müşavir",
                Core.Enums.MeslekTuru.Diger => string.IsNullOrWhiteSpace(u.MeslekTuruDiger)
                    ? "Diğer"
                    : $"Diğer: {u.MeslekTuruDiger}",
                _ => null
            };

            return new UserListItem(
                Id: u.Id,
                Email: u.Email ?? "",
                FullName: u.FullName,
                RoleName: roleMap.TryGetValue(u.Id, out var role) ? role : null,
                IsActive: u.IsActive,
                CreatedAt: u.CreatedAt,
                LastLoginAt: u.LastLoginAt,
                MeslekTuruLabel: meslekLabel);
        }).ToList();

        return new UserListPage(items, totalCount, page, pageSize);
    }
}
