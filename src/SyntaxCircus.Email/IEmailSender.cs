namespace SyntaxCircus.Email;

/// <summary>
/// Delivers an email message asynchronously.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Delivers <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="cancellationToken">The token that cancels the delivery operation.</param>
    /// <returns>A task that completes after the sender has accepted or delivered the message.</returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
