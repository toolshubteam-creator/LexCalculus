using Hangfire.Dashboard;

namespace LexCalculus.Web.Infrastructure.Hangfire;

/// <summary>
/// Hangfire Dashboard'a sadece "Admin" rolündeki authenticated kullanıcılar erişir.
/// Dashboard kendi auth sistemini kullanır (cookie auth zaten ASP.NET Core'da kurulu).
/// </summary>
public sealed class AdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
