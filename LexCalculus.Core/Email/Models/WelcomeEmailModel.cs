namespace LexCalculus.Core.Email.Models;

public sealed class WelcomeEmailModel
{
    public required string DisplayName { get; init; }
    public required string SiteUrl { get; init; }
    public required string ProfileUrl { get; init; }
    public required string CalculatorsUrl { get; init; }
}
