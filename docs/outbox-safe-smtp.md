# Safe SMTP for durable outboxes

The package supplies transport only. Applications own durable queues, incident policy, scheduling,
secret storage and at-least-once delivery. No message ID guarantees server deduplication.

```csharp
var options = new SmtpOptions
{
    Host = "internal-relay.example.com",
    Port = 587,
    DefaultFrom = "monitor@example.com",
    TlsMode = SmtpTlsMode.StartTls,
    TotalSendTimeout = TimeSpan.FromSeconds(30),
    RetryMode = SmtpRetryMode.TransientOnly,
    MaxRetryAttempts = 1,
};
var message = new EmailMessage("operator@example.com", "Monitoring incident", "Review the incident")
{
    MessageId = "persisted-outbox-id@example.com",
};
```

Persist the message ID before sending and reuse it after restarts. `MessageId` is an optional
init-only property; the existing positional constructor and deconstruction remain unchanged.
The sender constructs one MIME message per logical send and retains it across attempts.
MimeKit validates and formats the supplied ID. Null retains generated identity behavior.

`TlsMode` accepts `None`, `Auto`, `StartTls`, `SslOnConnect` or `StartTlsWhenAvailable`.
Null preserves `UseStartTls=true` → StartTls and false → Auto. Choose required TLS for a
protected relay. Invalid numeric TLS/retry values fail before opening a client.

`TotalSendTimeout` defaults to null. A positive value at most 4,294,967,294 milliseconds starts
after options retrieval and MIME construction, and covers connection, authentication, submission,
disconnect and exponential retry delays. Provider retrieval must honor the caller's cancellation
and any application deadline. MailKit operations honor the deadline token; custom client factories
must also honor it. Cancellation/disposal of arbitrary non-cooperating custom clients cannot be
given a hard execution guarantee. Caller cancellation takes precedence over a simultaneous timeout.

To bound the whole logical operation including options retrieval, create a linked cancellation
source before calling `SendAsync`, call `CancelAfter(TimeSpan.FromSeconds(30))`, and pass its token
to the sender. Keep `TotalSendTimeout=30 seconds` as the transport limit as well. The provider
receives the original caller token. Synchronous MIME construction cannot be interrupted mid-call;
the transport checks cancellation before creating a client. Map provider/MIME errors to sanitized
application outcomes and never log their raw exception text.

`RetryMode=Legacy` is the default: all non-cancellation transport failures retry, with original
final exceptions preserved. `TransientOnly` retries only known transient failures with confirmed
non-acceptance: SMTP 4xx rejections, network/IO/protocol failures before submission. Authentication,
5xx rejections and unknown failures do not retry. IO/protocol failures during submission stop
immediately because acceptance is uncertain. Delays remain 2^attempt seconds; total attempts still
clamp to one or greater.

In transient-only mode, `SmtpDeliveryException.Kind` is `Transient`, `Permanent`, `Authentication`,
`Timeout` or `Unknown`, and `AcceptanceUncertain` reports possible remote acceptance. No raw
server response or original inner exception is retained. Transport cancellation uses
`SmtpDeliveryCanceledException`, derived from `OperationCanceledException`, with the caller token
and `AcceptanceUncertain`. Cancellation during submission is uncertain. Options-provider and
MIME-validation errors remain unchanged; applications must sanitize those at their boundary.
An optional timeout produces a safe `SmtpDeliveryException` even with legacy retry mode.

A returned SMTP submission is accepted: disconnect/disposal failures, including cleanup
cancellation, produce safe warnings and cannot turn it into a retry or failed delivery.
Disposal failures during failed attempts cannot mask the original failure. This correctness fix
applies in both modes. Retry logs in both modes contain attempt/count/delay/category only; they
never attach the original exception, host, credentials, recipient, subject or body. Legacy callers
must avoid logging their raw final exceptions.

For durable outboxes use `MaxRetryAttempts=1`, a protected `ISmtpOptionsProvider`, an explicit TLS
mode and a bounded timeout. Track uncertain attempts separately and apply application retry policy
with duplicate-delivery awareness. The old constructor, registrations, provider precedence, one
options snapshot per send and fresh clients per attempt are preserved.
