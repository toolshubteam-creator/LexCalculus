namespace LexCalculus.Core.Email.Models;

public sealed class PasswordResetModel
{
    public required string DisplayName { get; init; }
    public required string ResetUrl { get; init; }
    public required string SiteUrl { get; init; }
}
