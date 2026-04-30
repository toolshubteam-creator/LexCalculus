using LexCalculus.Core.Entities.Identity;
using LexCalculus.Infrastructure.Data;
using LexCalculus.Infrastructure.Tenancy;
using LexCalculus.Web.Models.Layout;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Web.ViewComponents;

/// <summary>
/// Header'da kullanıcının tenant bağlamını render eder:
/// - Tenant üyesi: tenant adı + Owner/Üye etiketi + "Tenant Yönetimi" linki
/// - Bireysel kullanıcı: "Tenant Talebim" linki
/// - Anonim: hiçbir şey
/// P3/5 tech-debt'ini temizler (_Layout içinde inline UserManager.GetUserAsync).
/// </summary>
public sealed class UserTenantContextViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public UserTenantContextViewComponent(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _users.GetUserAsync((System.Security.Claims.ClaimsPrincipal)UserClaimsPrincipal!);
        if (user == null)
            return View(new UserTenantContextVm { IsAuthenticated = false });

        var vm = new UserTenantContextVm
        {
            IsAuthenticated = true,
            UserId = user.Id,
            HasTenant = user.TenantId.HasValue
        };

        if (user.TenantId.HasValue)
        {
            // AsAdminQuery: tenant filter bypass; üye olmasak da kendi tenant'ımızı görmeliyiz.
            // (Pratikte aynı tenant'tayız zaten ama defansif.)
            var tenant = await _db.Tenants
                .AsAdminQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);

            if (tenant != null && !tenant.IsDeleted)
            {
                vm.TenantId = tenant.Id;
                vm.TenantName = tenant.Name;
                vm.IsOwner = tenant.OwnerUserId == user.Id;
            }
            else
            {
                // Tenant silinmiş ama user.TenantId temizlenmemiş — defansif:
                // bireysel gibi davran, link gösterme.
                vm.HasTenant = false;
            }
        }

        return View(vm);
    }
}
