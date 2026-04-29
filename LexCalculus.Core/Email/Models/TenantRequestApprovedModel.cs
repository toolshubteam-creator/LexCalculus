namespace LexCalculus.Core.Email.Models;

public sealed class TenantRequestApprovedModel
{
    public required string DisplayName { get; init; }
    public required string TenantName { get; init; }
    public required string TenantSlug { get; init; }
    public required string SiteUrl { get; init; }
}
