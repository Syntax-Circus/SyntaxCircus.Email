using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SyntaxCircus.Email;

/// <summary>
/// Sends via SMTP using MailKit, with configurable retry policy and exponential backoff
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
    /// SMTP failures follow <see cref="SmtpOptions.RetryMode"/> with exponential delays until
    /// <see cref="SmtpOptions.MaxRetryAttempts"/> is exhausted. Successful SMTP acceptance remains
    /// successful if disconnect or disposal fails. A configured deadline starts after options
    /// retrieval and MIME construction and includes transport operations and retry delays.
    /// </remarks>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = await optionsProvider.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.RetryMode)) throw new InvalidOperationException("Invalid SMTP retry mode.");
        var socketOptions = settings.TlsMode switch
        {
            null => settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
            SmtpTlsMode.None => SecureSocketOptions.None,
            SmtpTlsMode.Auto => SecureSocketOptions.Auto,
            SmtpTlsMode.StartTls => SecureSocketOptions.StartTls,
            SmtpTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpTlsMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => throw new InvalidOperationException("Invalid SMTP TLS mode."),
        };
        if (settings.TotalSendTimeout is { } timeout && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1))
            throw new InvalidOperationException("Invalid SMTP total send timeout.");
        var mimeMessage = BuildMimeMessage(message, settings.DefaultFrom);
        using var deadline = settings.TotalSendTimeout is null ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (settings.TotalSendTimeout is { } budget) deadline!.CancelAfter(budget);
        var token = deadline?.Token ?? cancellationToken;

        var maxAttempts = Math.Max(1, settings.MaxRetryAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            MailKit.Net.Smtp.ISmtpClient? client = null;
            var sending = false;
            var accepted = false;
            try
            {
                token.ThrowIfCancellationRequested();
                client = smtpClientFactory.Create();
                await client.ConnectAsync(
                    settings.Host,
                    settings.Port,
                    socketOptions,
                    token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, token).ConfigureAwait(false);
                }

                sending = true;
                await client.SendAsync(mimeMessage, token).ConfigureAwait(false);
                accepted = true;
                try { await client.DisconnectAsync(true, token).ConfigureAwait(false); }
                catch (Exception) { logger.LogWarning("SMTP cleanup failed after accepted submission."); }
                return;
            }
            catch (Exception ex)
            {
                var uncertain = sending && ex is not SmtpCommandException;
                var kind = Classify(ex);
                if (ex is OperationCanceledException)
                {
                    if (!cancellationToken.IsCancellationRequested && deadline?.IsCancellationRequested == true)
                        throw new SmtpDeliveryException(SmtpFailureKind.Timeout, uncertain);
                    if (settings.RetryMode == SmtpRetryMode.TransientOnly)
                        throw new SmtpDeliveryCanceledException(uncertain, cancellationToken);
                    throw;
                }
                var retry = attempt < maxAttempts && (settings.RetryMode == SmtpRetryMode.Legacy ||
                    (kind == SmtpFailureKind.Transient && !uncertain));
                if (!retry)
                {
                    if (settings.RetryMode == SmtpRetryMode.TransientOnly) throw new SmtpDeliveryException(kind, uncertain);
                    throw;
                }
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning("SMTP send attempt {Attempt}/{MaxAttempts} failed ({Kind}); retrying in {Delay}.", attempt, maxAttempts, kind, delay);
                try { await Task.Delay(delay, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline?.IsCancellationRequested == true)
                { throw new SmtpDeliveryException(SmtpFailureKind.Timeout, uncertain); }
                catch (OperationCanceledException) when (settings.RetryMode == SmtpRetryMode.TransientOnly)
                { throw new SmtpDeliveryCanceledException(uncertain, cancellationToken); }
            }
            finally
            {
                try { client?.Dispose(); }
                catch (Exception) { logger.LogWarning("SMTP client disposal failed; submission accepted: {Accepted}.", accepted); }
            }
        }
    }

    private static SmtpFailureKind Classify(Exception exception) => exception switch
    {
        MailKit.Security.AuthenticationException => SmtpFailureKind.Authentication,
        SmtpCommandException smtp when (int)smtp.StatusCode is >= 400 and < 500 => SmtpFailureKind.Transient,
        SmtpCommandException smtp when (int)smtp.StatusCode is >= 500 and < 600 => SmtpFailureKind.Permanent,
        IOException or System.Net.Sockets.SocketException or SmtpProtocolException => SmtpFailureKind.Transient,
        _ => SmtpFailureKind.Unknown,
    };

    private static MimeMessage BuildMimeMessage(EmailMessage message, string defaultFrom)
    {
        var mimeMessage = new MimeMessage();
        if (message.MessageId is not null) mimeMessage.MessageId = message.MessageId;
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

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mimeMessage.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
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

        foreach (var attachment in message.Attachments ?? [])
        {
            bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();
        return mimeMessage;
    }
}
