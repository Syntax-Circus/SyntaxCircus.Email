namespace SyntaxCircus.Email;

/// <summary>
/// Describes one file attached to an <see cref="EmailMessage"/>.
/// </summary>
/// <param name="FileName">The attachment's file name, as presented to the recipient.</param>
/// <param name="Content">The attachment's raw bytes.</param>
/// <param name="ContentType">The attachment's MIME content type (e.g. <c>"application/pdf"</c>).</param>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
