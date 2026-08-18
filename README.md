# SyntaxCircus.Email

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Email/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Email/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Email.svg)](https://www.nuget.org/packages/SyntaxCircus.Email)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

`SyntaxCircus.Email` is a small .NET email-delivery primitive. It supplies an `IEmailSender`
abstraction with SMTP/MailKit, null/logging, and in-memory implementations. It intentionally does
not provide templates, rendering, queueing, delivery tracking, or application-specific message
composition.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs
> are welcome, but there is no SLA.

## Choose an implementation

| Environment or need | Registration | Behavior |
| --- | --- | --- |
| Production SMTP | `AddSmtpEmailSender(configuration)` | Sends through MailKit SMTP with exponential retry. |
| Local development | `AddNullEmailSender()` | Logs recipient and subject; sends no email. |
| Application tests | `AddInMemoryEmailSender()` | Captures messages in memory; sends no email. |
| Runtime-managed SMTP settings | Register `ISmtpOptionsProvider`, then call `AddSmtpEmailSender`. | Resolves one complete SMTP snapshot for each send. |

Register exactly one `IEmailSender` implementation for the service provider that sends mail.

## Quick start

```csharp
builder.Services.AddSmtpEmailSender(builder.Configuration);
```

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "Username": "smtp-user",
      "Password": "store-this-in-a-secret-provider",
      "UseStartTls": true,
      "DefaultFrom": "noreply@example.com",
      "MaxRetryAttempts": 3
    }
  }
}
```

```csharp
public sealed class WelcomeEmailService(IEmailSender emailSender)
{
    public Task SendWelcomeAsync(string to, CancellationToken cancellationToken)
        => emailSender.SendAsync(
            new EmailMessage(to, "Welcome!", "<p>Thanks for joining.</p>"),
            cancellationToken);
}
```

For a paired HTML and plain-text message, supply `PlainTextBody` while leaving `IsBodyHtml` as
`true`:

```csharp
await emailSender.SendAsync(new EmailMessage(
    "person@example.com",
    "Welcome!",
    "<p>Thanks for joining.</p>",
    PlainTextBody: "Thanks for joining."),
    cancellationToken);
```

## Documentation

- [Package guide](docs/package-guide.md) - public API, sender selection, message semantics, SMTP
  behavior, retries, cancellation, concurrency, and operational boundaries.
- [Runtime SMTP options](docs/runtime-smtp-options.md) - static versus database/secret-backed SMTP
  settings, provider lifecycle, registration order, security, and migration.
- [Testing guide](docs/testing-guide.md) - null and in-memory senders, assertions, and advanced
  transport test seams.
- [Agent instructions](https://github.com/Syntax-Circus/SyntaxCircus.Email/blob/main/AGENTS.md) -
  repository contracts for AI agents and automated contributors.

## Security note

Do not commit SMTP passwords. Use a secret provider or an application-owned
`ISmtpOptionsProvider`, and never log `SmtpOptions.Password` or decrypted credentials.

## Contributing

Keep changes focused, match `.editorconfig`, update the relevant package and agent documentation,
and call out public API or behavior changes in pull requests.

## License

MIT - see [LICENSE.txt](LICENSE.txt).
