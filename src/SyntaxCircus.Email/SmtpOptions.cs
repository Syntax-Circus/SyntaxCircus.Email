namespace SyntaxCircus.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseStartTls { get; set; } = true;

    public string DefaultFrom { get; set; } = string.Empty;

    public int MaxRetryAttempts { get; set; } = 3;
}
