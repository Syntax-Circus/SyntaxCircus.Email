# Runtime SMTP options

This guide supplements the [package guide](package-guide.md) with the contract for applications
whose SMTP settings are owned and changed at runtime.

`SyntaxCircus.Email` supports two sources of the complete SMTP connection profile:

| Source | Use when | Registration |
| --- | --- | --- |
| Static configuration | SMTP settings are deployed as configuration. | `AddSmtpEmailSender(builder.Configuration)` |
| `ISmtpOptionsProvider` | Settings are read, decrypted, or selected at send time. | Register the provider before `AddSmtpEmailSender`. |

The static configuration path is the default and remains compatible with existing applications.

## Dynamic provider

Implement `ISmtpOptionsProvider` when SMTP settings must be loaded from an application-owned
source such as a database, tenant configuration service, or secret store:

```csharp
public sealed class DatabaseSmtpOptionsProvider(
    IEmailSettingsRepository repository,
    ICredentialEncryptionService encryptionService) : ISmtpOptionsProvider
{
    public async ValueTask<SmtpOptions> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetSmtpSettingsAsync(cancellationToken);

        return new SmtpOptions
        {
            Host = settings.Host,
            Port = settings.Port,
            Username = settings.Username,
            Password = await encryptionService.DecryptAsync(settings.EncryptedPassword, cancellationToken),
            UseStartTls = settings.UseStartTls,
            DefaultFrom = settings.DefaultFrom,
            MaxRetryAttempts = settings.MaxRetryAttempts,
        };
    }
}
```

Register it before the package extension. `AddSmtpEmailSender` uses `TryAddSingleton`, so a
previous application registration wins:

```csharp
builder.Services.AddSingleton<ISmtpOptionsProvider, DatabaseSmtpOptionsProvider>();
builder.Services.AddSmtpEmailSender(builder.Configuration);
```

Do not register the custom provider after `AddSmtpEmailSender`: the package’s static fallback has
already been registered and is intentionally not replaced.

## Contract and behavior

`GetOptionsAsync` returns a **complete** `SmtpOptions` value, not credentials alone. Its result
controls host, port, TLS behavior, username, password, default sender, and retry count.

- `SmtpEmailSender` calls the provider once at the beginning of each `SendAsync`.
- One retrieved snapshot is reused for all attempts of that send, including retries.
- The `CancellationToken` passed to `SendAsync` is passed to the provider. Providers should honor
  it and should not replace cancellation with a successful fallback.
- A provider exception or cancellation is propagated before an SMTP client is created; it is not
  retried by SMTP retry logic.
- Existing static configuration still uses the same configuration section, `Email:Smtp`, and has
  the same effective behavior as before.
- The retry policy remains exponential (`2^attempt` seconds) and is controlled by
  `SmtpOptions.MaxRetryAttempts`. Custom retry scheduling is not part of this extension point.

For settings that can change at runtime, treat each returned `SmtpOptions` instance as an immutable
snapshot after return. Construct a new instance rather than mutating an instance that could be in
use by an in-flight send.

## Dependency-injection lifetime

`IEmailSender` is registered as a singleton. Its options provider must therefore be safe to call
concurrently. A singleton provider is appropriate when it depends only on singleton, thread-safe
services. If settings access needs scoped infrastructure such as an EF Core `DbContext`, inject
`IServiceScopeFactory` into the singleton provider and create/dispose a scope inside
`GetOptionsAsync`, or expose a thread-safe application service that owns the scoped work. Do not
inject a scoped service directly into a singleton provider.

The package preserves direct construction for existing consumers:

```csharp
var sender = new SmtpEmailSender(
    Microsoft.Extensions.Options.Options.Create(staticOptions),
    logger,
    smtpClientFactory);
```

New direct construction can use the provider constructor instead:

```csharp
var sender = new SmtpEmailSender(optionsProvider, logger, smtpClientFactory);
```

## Security and operations

- Store passwords encrypted at rest and decrypt only inside the provider immediately before use.
- Never write `SmtpOptions.Password`, decrypted settings, or SMTP authentication payloads to logs,
  exceptions, telemetry, or test diagnostics.
- Use a secret manager or appropriate encrypted data store; do not place dynamic credentials in
  source code or documentation examples.
- Validate values in the application-owned provider or its backing configuration before production
  use. Provider failures surface to the caller of `SendAsync`, enabling the application’s normal
  alerting and delivery-failure handling.
- Rotation takes effect for the next `SendAsync`; an email already retrying continues with its
  original snapshot by design.

## Migration

1. Keep the existing `Email:Smtp` configuration while introducing and testing the provider.
2. Implement a provider that maps the runtime source to every relevant `SmtpOptions` property.
3. Register the provider before `AddSmtpEmailSender`.
4. Verify two consecutive sends can use distinct host/authentication/default-sender values.
5. Verify provider failures, cancellation, credential rotation, and concurrent sends in the
   consuming application.
6. Remove static credentials only after the runtime source is deployed and operationally monitored.

## AI/agent integration contract

Agents changing or generating integrations must preserve these invariants:

1. Use the canonical public seam, `ISmtpOptionsProvider.GetOptionsAsync(CancellationToken)`, not
   a per-message credential field, a service locator in `SmtpEmailSender`, or an options-monitor
   substitute.
2. Register a custom `ISmtpOptionsProvider` before `AddSmtpEmailSender`; do not replace
   `IEmailSender` merely to obtain runtime settings.
3. Return a complete, self-consistent `SmtpOptions` snapshot and resolve it once per logical send,
   before MIME construction and retry calculation.
4. Reuse that snapshot across retries. Do not query the provider on every attempt, which could
   combine a sender identity from one configuration version with credentials from another.
5. Propagate provider errors and cancellation. Do not silently fall back to static options,
   suppress failures, cache credentials without an explicit application policy, or log secrets.
6. Preserve the existing static registration and direct
   `SmtpEmailSender(IOptions<SmtpOptions>, ILogger<SmtpEmailSender>, ISmtpClientFactory)`
   constructor for compatibility.
7. Add or retain tests that prove provider registration precedence, one resolution per send,
   fresh settings across two sends, snapshot reuse on retries, cancellation propagation, and the
   static configuration path.

For package-wide sender, message, retry, and operational behavior, see the
[package guide](package-guide.md). For normal application-test patterns, see the
[testing guide](testing-guide.md).
