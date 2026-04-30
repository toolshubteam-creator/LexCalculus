namespace LexCalculus.Web.Models.Layout;

public sealed class UserTenantContextVm
{
    public bool IsAuthenticated { get; set; }
    public int UserId { get; set; }
    public bool HasTenant { get; set; }
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsOwner { get; set; }
}
