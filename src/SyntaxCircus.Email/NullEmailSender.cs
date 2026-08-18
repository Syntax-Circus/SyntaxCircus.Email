namespace SyntaxCircus.Email;

/// <summary>
/// Logs recipient and subject instead of delivering email. Intended for local development.
/// </summary>
public sealed partial class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Logs <paramref name="message"/> without delivering it.
    /// </summary>
    /// <param name="message">The message whose recipient and subject are logged.</param>
    /// <param name="cancellationToken">
    /// Unused. Present to implement <see cref="IEmailSender"/> consistently.
    /// </param>
    /// <returns>A completed task.</returns>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmailNotSent(logger, message.To, message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email not sent (NullEmailSender): To={To}, Subject={Subject}")]
    private static partial void LogEmailNotSent(ILogger logger, string to, string subject);
}
