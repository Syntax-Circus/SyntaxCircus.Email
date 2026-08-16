namespace SyntaxCircus.Email;

/// <summary>Logs instead of sending — for local development.</summary>
public sealed partial class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmailNotSent(logger, message.To, message.Subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email not sent (NullEmailSender): To={To}, Subject={Subject}")]
    private static partial void LogEmailNotSent(ILogger logger, string to, string subject);
}
