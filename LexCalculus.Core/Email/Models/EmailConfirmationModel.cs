namespace LexCalculus.Core.Email.Models;

public sealed class EmailConfirmationModel
{
    public required string DisplayName { get; init; }
    public required string ConfirmationUrl { get; init; }
    public required string SiteUrl { get; init; }
}
