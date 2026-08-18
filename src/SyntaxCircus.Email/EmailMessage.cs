namespace SyntaxCircus.Email;

/// <param name="To">
/// One recipient address, or multiple comma-separated addresses (e.g. "a@example.com,b@example.com")
/// to send a single message to more than one primary recipient.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml = true,
    string? From = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null);
