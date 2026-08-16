namespace SyntaxCircus.Email.Tests;

public class InMemoryEmailSenderTests
{
    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNullException()
    {
        var sender = new InMemoryEmailSender();

        await Should.ThrowAsync<ArgumentNullException>(() => sender.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_AccumulatesMessagesInOrder()
    {
        var sender = new InMemoryEmailSender();
        var first = new EmailMessage("a@example.com", "First", "Body");
        var second = new EmailMessage("b@example.com", "Second", "Body");

        await sender.SendAsync(first, TestContext.Current.CancellationToken);
        await sender.SendAsync(second, TestContext.Current.CancellationToken);

        sender.SentMessages.ShouldBe([first, second]);
    }

    [Fact]
    public async Task SendAsync_CompletesSynchronously()
    {
        var sender = new InMemoryEmailSender();

        var task = sender.SendAsync(new EmailMessage("a@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Clear_EmptiesSentMessages()
    {
        var sender = new InMemoryEmailSender();
        await sender.SendAsync(new EmailMessage("a@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        sender.Clear();

        sender.SentMessages.ShouldBeEmpty();
    }
}
