using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SyntaxCircus.Email;

/// <summary>
/// Sends via SMTP using MailKit, retrying transient failures with exponential backoff
/// (<see cref="SmtpOptions.MaxRetryAttempts"/>, default 3).
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger, ISmtpClientFactory smtpClientFactory) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        var mimeMessage = BuildMimeMessage(message, settings.DefaultFrom);

        var maxAttempts = Math.Max(1, settings.MaxRetryAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = smtpClientFactory.Create();
                await client.ConnectAsync(
                    settings.Host,
                    settings.Port,
                    settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                    cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
                }

                await client.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
                await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(ex, "SMTP send attempt {Attempt}/{MaxAttempts} failed; retrying in {Delay}.", attempt, maxAttempts, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static MimeMessage BuildMimeMessage(EmailMessage message, string defaultFrom)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(string.IsNullOrWhiteSpace(message.From) ? defaultFrom : message.From));
        mimeMessage.To.AddRange(InternetAddressList.Parse(message.To));

        foreach (var cc in message.Cc ?? [])
        {
            mimeMessage.Cc.Add(MailboxAddress.Parse(cc));
        }

        foreach (var bcc in message.Bcc ?? [])
        {
            mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder();
        if (message.IsBodyHtml)
        {
            bodyBuilder.HtmlBody = message.Body;
            if (!string.IsNullOrEmpty(message.PlainTextBody))
            {
                bodyBuilder.TextBody = message.PlainTextBody;
            }
        }
        else
        {
            bodyBuilder.TextBody = message.Body;
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();
        return mimeMessage;
    }
}
