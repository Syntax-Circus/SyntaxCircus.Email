using System.Collections.Concurrent;

namespace SyntaxCircus.Email;

/// <summary>
/// Captures sent messages in memory instead of delivering them. Intended for tests.
/// </summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>
    /// Gets a snapshot of messages captured by this sender.
    /// </summary>
    public IReadOnlyCollection<EmailMessage> SentMessages => [.. _sent];

    /// <summary>
    /// Captures <paramref name="message"/> without delivering it.
    /// </summary>
    /// <param name="message">The message to capture.</param>
    /// <param name="cancellationToken">
    /// Unused. Present to implement <see cref="IEmailSender"/> consistently.
    /// </param>
    /// <returns>A completed task.</returns>
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes all captured messages.
    /// </summary>
    public void Clear() => _sent.Clear();
}
