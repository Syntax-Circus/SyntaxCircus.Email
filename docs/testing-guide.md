# Testing with SyntaxCircus.Email

Use a sender implementation that matches the kind of test being written. Most application tests
should use `InMemoryEmailSender`; they should not mock MailKit or connect to a real SMTP server.

## Application tests with the in-memory sender

```csharp
var services = new ServiceCollection();
services.AddInMemoryEmailSender();

using var provider = services.BuildServiceProvider();
var sender = provider.GetRequiredService<IEmailSender>();

await sender.SendAsync(new EmailMessage(
    "person@example.com",
    "Welcome",
    "<p>Thanks for joining.</p>"));

var captured = provider.GetRequiredService<InMemoryEmailSender>();
Assert.Single(captured.SentMessages);
Assert.Equal("Welcome", captured.SentMessages.Single().Subject);
```

`InMemoryEmailSender` captures the original immutable `EmailMessage` values in a thread-safe
queue. `SentMessages` returns a snapshot suitable for assertions. Call `Clear()` when a shared
test fixture needs to reset capture state, but prefer a fresh service provider per test to avoid
cross-test state.

The in-memory sender does not parse addresses, build MIME, invoke providers, apply SMTP retries,
or validate SMTP configuration. Those behaviors belong to transport-level tests or to the
application component responsible for validating its input.

## Local development with the null sender

```csharp
services.AddNullEmailSender();
```

`NullEmailSender` logs an information message containing the message recipient and subject, then
returns a completed task. It is useful where mail should be visible in logs but not delivered. It
does not retain messages for later inspection, so use `InMemoryEmailSender` for assertions.

## Testing a runtime options provider

Test the application-owned provider separately. Verify that it:

1. Returns every relevant `SmtpOptions` property, not just credentials.
2. Honors the supplied cancellation token.
3. Does not log or expose decrypted credentials.
4. Produces a new, self-consistent snapshot for each call when settings can change.
5. Uses a safe lifetime strategy when its backing data access is scoped.

An integration test can register the provider before the SMTP extension and substitute
`ISmtpClientFactory` to confirm that the resolved options reach connection/authentication behavior:

```csharp
services.AddSingleton<ISmtpOptionsProvider, TestOptionsProvider>();
services.AddSingleton<ISmtpClientFactory>(smtpClientFactory);
services.AddSmtpEmailSender(configuration);
```

The registration order matters: `AddSmtpEmailSender` intentionally uses `TryAddSingleton` for the
default provider and client factory, so application registrations made first take precedence.

## Advanced SMTP transport tests

`ISmtpClientFactory` exists to create MailKit `ISmtpClient` instances. It is mainly a package test
seam for asserting connection, authentication, send, disconnect, retry, and cancellation behavior
without an SMTP server. Do not add it to ordinary business-service tests merely to assert that an
email was requested; use `InMemoryEmailSender` instead.

When testing extensions to this package, cover:

- default static configuration registration;
- custom `ISmtpOptionsProvider` precedence;
- fresh provider options across separate sends;
- one provider resolution reused throughout a retry sequence;
- no retry for provider failures or cancellation;
- malformed recipient behavior before client creation;
- authentication omitted for a blank username;
- multipart HTML/plain-text construction; and
- the retained legacy `SmtpEmailSender` constructor.

## Avoid real SMTP in unit tests

Real SMTP tests are integration tests with external credentials, rate limits, mailbox cleanup, and
delivery timing concerns. Keep them isolated from unit tests, inject credentials only through
approved secret mechanisms, and never run them by default in pull-request validation.
