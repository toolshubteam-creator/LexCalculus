using Microsoft.EntityFrameworkCore;

namespace LexCalculus.Infrastructure.Tenancy;

/// <summary>
/// Admin/cross-user sorgularda tenant filter'ı bilinçli olarak bypass eder.
/// Sadece bu metodu çağıran kod path'i yetki kontrolünü yapmış olmalıdır
/// (örn. <c>[Authorize(Roles="Admin")]</c> attribute'u). Defense-in-depth gereği
/// kullanılan her noktada yetki kontrolü call site'da görünür olmalıdır.
/// Ayrıca soft-delete filter'ı da bypass eder — gerekirse sorguda
/// <c>.Where(e =&gt; !e.IsDeleted)</c> elle eklenir.
/// </summary>
public static class AdminQueryExtensions
{
    public static IQueryable<T> AsAdminQuery<T>(this IQueryable<T> query) where T : class
        => query.IgnoreQueryFilters();
}
