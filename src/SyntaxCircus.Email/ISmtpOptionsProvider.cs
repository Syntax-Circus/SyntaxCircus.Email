namespace SyntaxCircus.Email;

/// <summary>
/// Retrieves the complete SMTP options snapshot used for one email send.
/// </summary>
/// <remarks>
/// Implementations may retrieve settings asynchronously from an application-owned source, such
/// as a database or secret store, and should honor the cancellation token supplied to
/// <see cref="GetOptionsAsync(CancellationToken)"/>. The
/// sender resolves options once at the beginning of each <see cref="IEmailSender.SendAsync"/>
/// operation and uses that snapshot for every retry of that operation.
/// </remarks>
public interface ISmtpOptionsProvider
{
    /// <summary>
    /// Gets the SMTP options to use for one email send.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels options retrieval.</param>
    /// <returns>A complete SMTP options snapshot for the send.</returns>
    ValueTask<SmtpOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
}
