# SyntaxCircus.Email

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Email/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Email/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

An email-sending abstraction: a MailKit-based SMTP implementation with retry, plus Null (dev) and in-memory (test) implementations. Templating and composition are out of scope on purpose — too product-specific to generalize; this only covers the send primitive and provider switch.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Usage

```csharp
builder.Services.AddSmtpEmailSender(builder.Configuration); // binds "Email:Smtp"
// or, in Development: builder.Services.AddNullEmailSender();
// or, in tests: builder.Services.AddInMemoryEmailSender();
```

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "Username": "...",
      "Password": "...",
      "DefaultFrom": "noreply@example.com",
      "MaxRetryAttempts": 3
    }
  }
}
```

```csharp
public sealed class WelcomeEmailService(IEmailSender emailSender)
{
    public Task SendWelcomeAsync(string to, CancellationToken ct)
        => emailSender.SendAsync(new EmailMessage(to, "Welcome!", "<p>Hi there.</p>"), ct);
}
```

`SmtpEmailSender` retries transient send failures with exponential backoff (`MaxRetryAttempts`, default 3). `InMemoryEmailSender` exposes `SentMessages` (and `Clear()`) for assertions in tests — resolve it directly (it's also registered as itself, not just as `IEmailSender`) when you need to inspect what was "sent".

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
