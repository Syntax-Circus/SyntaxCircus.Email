namespace SyntaxCircus.Email;

/// <param name="To">
/// One recipient address, or multiple comma-separated addresses (e.g. "a@example.com,b@example.com")
/// to send a single message to more than one primary recipient.
/// </param>
/// <param name="PlainTextBody">
/// An optional plain-text alternative view. Only used when <paramref name="IsBodyHtml"/> is
/// <see langword="true"/> — <paramref name="Body"/> is then sent as the HTML view and this as the
/// paired text view, as a <c>multipart/alternative</c> message. Ignored when
/// <paramref name="IsBodyHtml"/> is <see langword="false"/>, since there's no HTML view to pair it
/// with — <paramref name="Body"/> alone is sent as plain text in that case.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml = true,
    string? From = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null,
    string? PlainTextBody = null);
