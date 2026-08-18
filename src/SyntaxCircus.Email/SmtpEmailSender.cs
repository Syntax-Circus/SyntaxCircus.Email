using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SyntaxCircus.Email;

/// <summary>
/// Sends via SMTP using MailKit, retrying transient failures with exponential backoff
/// (<see cref="SmtpOptions.MaxRetryAttempts"/>, default 3).
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly ISmtpOptionsProvider optionsProvider;
    private readonly ILogger<SmtpEmailSender> logger;
    private readonly ISmtpClientFactory smtpClientFactory;

    /// <summary>
    /// Initializes a sender that retrieves a complete SMTP options snapshot for each email send.
    /// </summary>
    /// <param name="optionsProvider">The provider that retrieves one SMTP options snapshot per send.</param>
    /// <param name="logger">The logger that receives retry warnings.</param>
    /// <param name="smtpClientFactory">The factory that creates MailKit SMTP clients.</param>
    /// <remarks>
    /// The retrieved snapshot is retained for every retry of the same send operation. Register
    /// an <see cref="ISmtpOptionsProvider"/> before calling
    /// <see cref="EmailServiceCollectionExtensions.AddSmtpEmailSender(IServiceCollection, IConfiguration)"/>
    /// to use this behavior through dependency injection.
    /// </remarks>
    public SmtpEmailSender(
        ISmtpOptionsProvider optionsProvider,
        ILogger<SmtpEmailSender> logger,
        ISmtpClientFactory smtpClientFactory)
    {
        this.optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.smtpClientFactory = smtpClientFactory ?? throw new ArgumentNullException(nameof(smtpClientFactory));
    }

    /// <summary>
    /// Initializes a sender backed by static <see cref="SmtpOptions"/> from the options pattern.
    /// </summary>
    /// <param name="options">The static SMTP options.</param>
    /// <param name="logger">The logger that receives retry warnings.</param>
    /// <param name="smtpClientFactory">The factory that creates MailKit SMTP clients.</param>
    /// <remarks>
    /// This constructor is retained for compatibility. New applications that require runtime SMTP
    /// settings should use <see cref="ISmtpOptionsProvider"/>.
    /// </remarks>
    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger,
        ISmtpClientFactory smtpClientFactory)
        : this(new StaticSmtpOptionsProvider(options), logger, smtpClientFactory)
    {
    }

    /// <summary>
    /// Sends <paramref name="message"/> through SMTP.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token that cancels options retrieval, delivery, or retry delay.</param>
    /// <returns>A task that completes after SMTP accepts the message.</returns>
    /// <remarks>
    /// Options are resolved once before MIME construction and reused for all retries. Non-cancellation
    /// SMTP failures retry with exponential delays until <see cref="SmtpOptions.MaxRetryAttempts"/> is exhausted.
    /// </remarks>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = await optionsProvider.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(settings);
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
