namespace LexCalculus.Core.Email.Models;

public sealed class TenantInvitationEmailModel
{
    public required string TenantName { get; init; }
    public required string InvitedByUserName { get; init; }
    public required string Email { get; init; }
    public required string AcceptUrl { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string SiteUrl { get; init; }
}
