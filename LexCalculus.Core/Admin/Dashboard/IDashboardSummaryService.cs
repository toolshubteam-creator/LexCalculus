namespace LexCalculus.Core.Admin.Dashboard;

public interface IDashboardSummaryService
{
    /// <summary>
    /// Dashboard için 5 widget'ın özet verilerini çeker. Sorgular sequential
    /// çalışır (DbContext thread-safe değil). Bir widget query'si fail
    /// olursa o widget null döner, diğer widget'lar etkilenmez.
    /// </summary>
    Task<DashboardSummary> GetSummaryAsync(int currentAdminUserId, CancellationToken ct = default);
}
