namespace LexCalculus.Core.Email.Models;

public sealed class TenantRequestRejectedModel
{
    public required string DisplayName { get; init; }
    public required string ProposedName { get; init; }
    public required string Reason { get; init; }
    public required string SiteUrl { get; init; }
}
