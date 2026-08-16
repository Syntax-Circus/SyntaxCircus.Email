namespace SyntaxCircus.Email.Tests;

public class NullEmailSenderTests
{
    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNullException()
    {
        var sender = new NullEmailSender(Substitute.For<ILogger<NullEmailSender>>());

        await Should.ThrowAsync<ArgumentNullException>(() => sender.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_ValidMessage_CompletesWithoutThrowing()
    {
        var sender = new NullEmailSender(Substitute.For<ILogger<NullEmailSender>>());

        await Should.NotThrowAsync(() =>
            sender.SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken));
    }
}
