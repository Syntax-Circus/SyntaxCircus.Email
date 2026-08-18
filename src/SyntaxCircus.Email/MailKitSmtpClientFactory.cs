using MailKit.Net.Smtp;

namespace SyntaxCircus.Email;

/// <summary>
/// Creates the package's default MailKit SMTP clients.
/// </summary>
public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    /// <summary>
    /// Creates a new <see cref="SmtpClient"/>.
    /// </summary>
    /// <returns>A new MailKit SMTP client.</returns>
    public ISmtpClient Create() => new SmtpClient();
}
