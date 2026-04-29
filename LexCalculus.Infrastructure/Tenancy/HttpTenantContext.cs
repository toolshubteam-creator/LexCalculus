using System.Security.Claims;
using LexCalculus.Core.Services;
using Microsoft.AspNetCore.Http;

namespace LexCalculus.Infrastructure.Tenancy;

/// <summary>
/// HttpContext claims üzerinden ITenantContext implementation'u.
/// TenantId claim'i AppUserClaimsPrincipalFactory tarafından login sırasında
/// kullanıcının ApplicationUser.TenantId değerinden üretilir.
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? CurrentTenantId
    {
        get
        {
            var claim = _accessor.HttpContext?.User.FindFirst("TenantId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public int? CurrentUserId
    {
        get
        {
            var claim = _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsTenantMember => CurrentTenantId.HasValue;
}
