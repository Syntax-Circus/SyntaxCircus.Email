namespace SyntaxCircus.Email;

/// <summary>Explicit SMTP connection security.</summary>
public enum SmtpTlsMode
{
    /// <summary>No TLS.</summary>
    None,
    /// <summary>MailKit automatic security selection.</summary>
    Auto,
    /// <summary>Require STARTTLS.</summary>
    StartTls,
    /// <summary>Require TLS immediately on connection.</summary>
    SslOnConnect,
    /// <summary>Use STARTTLS when offered by the server.</summary>
    StartTlsWhenAvailable,
}

/// <summary>SMTP retry and error policy.</summary>
public enum SmtpRetryMode
{
    /// <summary>Retry all non-cancellation transport failures and preserve original final exceptions.</summary>
    Legacy,
    /// <summary>Retry only known transient failures before uncertain acceptance; expose sanitized typed errors.</summary>
    TransientOnly,
}

/// <summary>Sanitized transport failure category.</summary>
public enum SmtpFailureKind
{
    /// <summary>A temporary transport or SMTP rejection.</summary>
    Transient,
    /// <summary>A permanent SMTP rejection.</summary>
    Permanent,
    /// <summary>Authentication failed.</summary>
    Authentication,
    /// <summary>The total send deadline elapsed.</summary>
    Timeout,
    /// <summary>An unclassified failure; automatic retry is unsafe.</summary>
    Unknown,
}

/// <summary>A sanitized SMTP failure without the secret-bearing original exception.</summary>
public sealed class SmtpDeliveryException : Exception
{
    internal SmtpDeliveryException(SmtpFailureKind kind, bool acceptanceUncertain)
        : base($"SMTP delivery failed ({kind}); acceptance uncertain: {acceptanceUncertain}.")
    {
        Kind = kind;
        AcceptanceUncertain = acceptanceUncertain;
    }

    /// <summary>Gets the safe failure category.</summary>
    public SmtpFailureKind Kind { get; }

    /// <summary>Gets whether the remote server may have accepted the message. Stable Message-Id does not guarantee deduplication.</summary>
    public bool AcceptanceUncertain { get; }
}

/// <summary>Sanitized cancellation in transient-only mode, retaining uncertain submission state.</summary>
public sealed class SmtpDeliveryCanceledException : OperationCanceledException
{
    internal SmtpDeliveryCanceledException(bool acceptanceUncertain, CancellationToken cancellationToken)
        : base("SMTP delivery canceled.", cancellationToken)
    {
        AcceptanceUncertain = acceptanceUncertain;
    }

    /// <summary>Gets whether cancellation occurred during a submission that may have been accepted.</summary>
    public bool AcceptanceUncertain { get; }
}
