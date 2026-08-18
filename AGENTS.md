# SyntaxCircus.Email agent instructions

## Package purpose and boundary

This repository provides a deliberately narrow .NET email-delivery primitive. It owns message
transport selection and SMTP delivery only. Do not add template rendering, localization, HTML
sanitization, attachment policy, queueing, durable retry orchestration, delivery tracking, or
application-specific sender/recipient authorization without an explicit request and a separate
design.

Read [README.md](README.md) for navigation, [docs/package-guide.md](docs/package-guide.md) for
canonical behavior, [docs/runtime-smtp-options.md](docs/runtime-smtp-options.md) for dynamic SMTP
settings, and [docs/testing-guide.md](docs/testing-guide.md) for test boundaries.

## Canonical public API

- `IEmailSender.SendAsync(EmailMessage, CancellationToken)` is the only application-facing send
  abstraction.
- `EmailMessage` is immutable. `To` may be comma-separated; `Cc`/`Bcc` are individual entries;
  `PlainTextBody` is meaningful only for HTML messages.
- `AddSmtpEmailSender(IConfiguration)`, `AddNullEmailSender()`, and
  `AddInMemoryEmailSender()` are the supported registrations.
- `SmtpOptions.SectionName` is `Email:Smtp`.
- `ISmtpOptionsProvider.GetOptionsAsync(CancellationToken)` supplies a complete SMTP snapshot,
  not credentials alone.
- `ISmtpClientFactory` is an advanced transport-test seam, not a default application extension
  point.

## Non-breaking behavior requirements

1. Preserve `IEmailSender`, `EmailMessage`, `SmtpOptions`, and all existing registration method
   signatures unless an explicitly approved breaking change says otherwise.
2. Preserve the static configuration path and the public legacy constructor:
   `SmtpEmailSender(IOptions<SmtpOptions>, ILogger<SmtpEmailSender>, ISmtpClientFactory)`.
3. Retain custom `ISmtpOptionsProvider` and `ISmtpClientFactory` registrations made before
   `AddSmtpEmailSender`; the extension uses `TryAddSingleton` defaults.
4. Resolve dynamic SMTP options once per `SendAsync`, before MIME construction and retry
   calculation. Reuse that snapshot for every retry of the same message.
5. Preserve current SMTP behavior: new client per attempt, authentication only for non-blank
   usernames, null password becomes empty, `UseStartTls` maps to `StartTls` or `Auto`, and retry
   attempts clamp to at least one with `2^attempt`-second delays.
6. Propagate malformed-address, options-provider, SMTP, and cancellation failures. Do not add
   silent fallbacks, broad catches, credential caching, or success-shaped error handling.

## Dependency injection and concurrency

All supplied senders are singletons. Any custom `ISmtpOptionsProvider` must be safe for concurrent
calls. Do not inject a scoped dependency directly into a singleton provider; create a scope inside
the provider or use a thread-safe application service. Register one sender implementation per
consumer service provider unless deliberate last-registration-wins behavior is required and
documented.

## Security and logging

Never commit, emit, or log SMTP passwords, decrypted settings, authentication exchanges, or
unnecessary message content. Document examples with placeholders. Treat provider-returned settings
as an immutable in-flight snapshot. Do not weaken cancellation propagation or transport error
visibility.

## Tests and documentation

Use `InMemoryEmailSender` for application-level assertions. Reserve `ISmtpClientFactory` fakes for
package-level transport behavior. Changes to SMTP/provider code must retain or add coverage for
static registration, custom-provider precedence, fresh settings per send, one snapshot per retry,
cancellation, final error propagation, and legacy construction.

Update the relevant README and `docs` guide whenever public behavior, configuration, examples, or
operational constraints change. Update XML comments for every changed public member. Keep
`README.md` as the landing page and detailed behavior in the focused guides.
