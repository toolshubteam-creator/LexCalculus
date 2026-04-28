namespace LexCalculus.Core.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Logging";
    public string From { get; set; } = "noreply@lexcalculus.local";
    public string FromDisplayName { get; set; } = "Lex Calculus";
    public SmtpOptions Smtp { get; set; } = new();
    public SendGridOptions SendGrid { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = false;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = "";
}
