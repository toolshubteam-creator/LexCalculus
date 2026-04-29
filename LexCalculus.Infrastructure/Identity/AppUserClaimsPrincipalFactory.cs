using System.Security.Claims;
using LexCalculus.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LexCalculus.Infrastructure.Identity;

/// <summary>
/// Login sırasında ApplicationUser.TenantId değerini "TenantId" claim'i
/// olarak ClaimsIdentity'ye yazar. HttpTenantContext bu claim'i okuyarak
/// CurrentTenantId değerini üretir.
/// Faz 3.7 — multi-tenant altyapı.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.TenantId.HasValue)
            identity.AddClaim(new Claim("TenantId", user.TenantId.Value.ToString()));
        return identity;
    }
}
