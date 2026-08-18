using MailKit.Net.Smtp;

namespace SyntaxCircus.Email;

/// <summary>
/// Creates the MailKit <see cref="ISmtpClient"/> <see cref="SmtpEmailSender"/> connects through.
/// The seam this package's own tests substitute to exercise connect/auth/send/retry logic without
/// a real SMTP server — not something most consumers need to touch.
/// </summary>
public interface ISmtpClientFactory
{
    /// <summary>
    /// Creates an SMTP client for one connection attempt.
    /// </summary>
    /// <returns>A new MailKit SMTP client.</returns>
    ISmtpClient Create();
}
