namespace SyntaxCircus.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml = true,
    string? From = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null);
