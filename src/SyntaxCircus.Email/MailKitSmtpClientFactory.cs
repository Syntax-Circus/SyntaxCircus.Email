using MailKit.Net.Smtp;

namespace SyntaxCircus.Email;

public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new SmtpClient();
}
