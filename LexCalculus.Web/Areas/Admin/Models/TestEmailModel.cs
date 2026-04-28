namespace LexCalculus.Web.Areas.Admin.Models;

public sealed class TestEmailModel
{
    public required string RecipientName { get; init; }
    public required string Provider { get; init; }
    public required DateTime SentAt { get; init; }
    public required string MachineName { get; init; }
}
