namespace SyntaxCircus.Email;

internal sealed class StaticSmtpOptionsProvider(IOptions<SmtpOptions> options) : ISmtpOptionsProvider
{
    public ValueTask<SmtpOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(options.Value);
}
