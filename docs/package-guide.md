# SyntaxCircus.Email package guide

This guide describes the complete public behavior of `SyntaxCircus.Email`. It is a delivery
primitive: applications create an `EmailMessage` and delegate transport to `IEmailSender`.
Applications remain responsible for templates, localization, queueing, delivery status, auditing,
and product-specific sender-selection rules.

## Public model

| API | Purpose |
| --- | --- |
| `IEmailSender` | Asynchronous contract for delivering one `EmailMessage`. |
| `EmailMessage` | Immutable recipient, subject, body, addressing, and body-format data. |
| `SmtpEmailSender` | SMTP implementation based on MailKit. |
| `NullEmailSender` | Development implementation that logs instead of delivering. |
| `InMemoryEmailSender` | Test implementation that retains sent messages in memory. |
| `SmtpOptions` | Complete SMTP connection, authentication, sender, TLS, and retry settings. |
| `ISmtpOptionsProvider` | Async source for a complete SMTP options snapshot per send. |
| `ISmtpClientFactory` | Advanced MailKit client seam, primarily for package transport tests. |

All supplied senders are registered as singletons by their service-collection extension methods.
Applications may call `IEmailSender.SendAsync` concurrently; custom dynamic-option providers must
be concurrency-safe as well.

## Selecting and registering a sender

### SMTP

```csharp
builder.Services.AddSmtpEmailSender(builder.Configuration);
```

This binds `Email:Smtp` to `SmtpOptions`, registers MailKit’s client factory unless the
application already supplied one, and registers `IEmailSender` as `SmtpEmailSender`.

### Development null sender

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddNullEmailSender();
}
```

`NullEmailSender` validates that the message is non-null, logs its recipient and subject at
information level, and completes successfully. It never opens a connection or delivers mail. Do
not use it as a production fallback unless silently dropping email is an explicit product decision.

### In-memory test sender

```csharp
services.AddInMemoryEmailSender();
```

`InMemoryEmailSender` stores each submitted `EmailMessage` in a thread-safe queue. It is
registered both as `IEmailSender` and as itself, so tests can resolve it and inspect
`SentMessages`. See the [testing guide](testing-guide.md).

Registering multiple sender extensions registers multiple `IEmailSender` services. Standard .NET
DI resolves the most recently registered service for a single `IEmailSender` request. Prefer an
explicit environment branch that registers exactly one sender, rather than relying on registration
order as an application policy.

## Creating messages

```csharp
var message = new EmailMessage(
    To: "primary@example.com,second@example.com",
    Subject: "Monthly report",
    Body: "<p>Your report is ready.</p>",
    IsBodyHtml: true,
    From: "reports@example.com",
    Cc: ["manager@example.com"],
    Bcc: ["archive@example.com"],
    PlainTextBody: "Your report is ready.");
```

`To` accepts one address or a comma-separated list for the primary recipient list. `Cc` and `Bcc`
are individual address entries. SMTP address parsing is delegated to MimeKit; malformed addresses
cause `SendAsync` to fail before an SMTP client is created.

`From` overrides `SmtpOptions.DefaultFrom` for an individual message. When neither supplies a
valid address, MimeKit parsing fails. The package does not validate domain ownership, recipient
authorization, HTML safety, or message size.

When `IsBodyHtml` is `true` (the default), `Body` is the HTML view. If `PlainTextBody` is also
provided, MailKit constructs a `multipart/alternative` message. When `IsBodyHtml` is `false`,
`Body` is a plain-text message and `PlainTextBody` is ignored.

## SMTP configuration and delivery behavior

The default static configuration section is:

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "Username": "optional-user",
      "Password": "optional-password",
      "UseStartTls": true,
      "DefaultFrom": "noreply@example.com",
      "MaxRetryAttempts": 3
    }
  }
}
```

| `SmtpOptions` setting | Default | Effect |
| --- | --- | --- |
| `Host` | Empty | SMTP host passed to MailKit. |
| `Port` | `587` | SMTP port passed to MailKit. |
| `Username` | `null` | Enables SMTP authentication when non-blank. |
| `Password` | `null` | Used with `Username`; a null password becomes an empty string. |
| `UseStartTls` | `true` | Uses MailKit `StartTls`; `false` uses MailKit `Auto`. |
| `DefaultFrom` | Empty | Sender when `EmailMessage.From` is absent or blank. |
| `MaxRetryAttempts` | `3` | Total attempts; zero or negative values are clamped to one. |

For every SMTP send, the sender builds the MIME message, opens a new MailKit client, connects,
optionally authenticates, sends, and disconnects. A client is not reused between sends or retries.
The package does not pool connections or support attachments, custom MIME headers, DKIM, or
delivery receipts.

## Retries, failures, and cancellation

SMTP failures other than `OperationCanceledException` are retried until the configured total
attempt count is exhausted. Delays are exponential: retry after attempt 1 waits two seconds, then
four seconds after attempt 2, and so on. The final failure propagates to the `SendAsync` caller.

Cancellation is passed to option retrieval, connection, authentication, send, disconnect, and
retry delay. Cancellation is not retried. Invalid messages and provider failures also propagate;
there is no silent fallback sender or success-shaped error handling.

The package cannot determine whether a transport failure happened before or after a remote SMTP
server accepted a message. Consumers that need exactly-once delivery, durable retries, or delivery
tracking must provide those policies outside this package.

## Runtime SMTP settings

Static configuration is sufficient when SMTP settings change only with deployment configuration.
Use `ISmtpOptionsProvider` for database-backed, decrypted, tenant-specific, or rotating settings.
The provider returns the entire connection profile and is called once at the start of each logical
send. Its returned snapshot remains fixed for all retries of that send.

Read the [runtime SMTP options guide](runtime-smtp-options.md) before implementing a provider. It
defines registration ordering, provider lifetime, cancellation, security, rotation, and migration
requirements.

## Security and operations

- Store secrets outside source control and encrypt persisted credentials at rest.
- Avoid logging passwords, decrypted settings, SMTP authentication payloads, or full email bodies.
- Treat HTML content, recipient addresses, and sender overrides as application input that needs
  application-specific authorization and sanitization.
- Monitor exceptions from `SendAsync`; this package logs retry warnings but does not report
  delivery health or suppress final failures.
- Set the `From` address to an identity accepted by the configured SMTP service.

## Compatibility commitments

The package preserves the static `Email:Smtp` path and
`SmtpEmailSender(IOptions<SmtpOptions>, ILogger<SmtpEmailSender>, ISmtpClientFactory)` constructor.
The provider constructor is additive. Consumers relying on either path should not need to change
when adding a custom `ISmtpOptionsProvider`.

For agent and contributor rules that protect these commitments, see
[AGENTS.md](https://github.com/Syntax-Circus/SyntaxCircus.Email/blob/main/AGENTS.md).
