namespace SyntaxCircus.Email;

/// <summary>
/// Configures SMTP connection, authentication, sender, TLS, and retry behavior.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>
    /// The configuration section name used by <see cref="EmailServiceCollectionExtensions.AddSmtpEmailSender"/>.
    /// </summary>
    public const string SectionName = "Email:Smtp";

    /// <summary>
    /// Gets or sets the SMTP host name.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SMTP port. The default is 587.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Gets or sets the optional SMTP username. A null, empty, or whitespace value skips authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the optional SMTP password used with <see cref="Username"/>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether to require MailKit's StartTLS connection option. The default is
    /// <see langword="true"/>; <see langword="false"/> uses MailKit's automatic socket option.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Gets or sets the sender address used when <see cref="EmailMessage.From"/> is null, empty, or whitespace.
    /// </summary>
    public string DefaultFrom { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total SMTP delivery attempts. Values below one are treated as one attempt.
    /// The default is three.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
