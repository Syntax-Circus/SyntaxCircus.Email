using MailKit.Security;
using MimeKit;

namespace SyntaxCircus.Email.Tests;

public class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender(SmtpOptions options, ISmtpClientFactory factory)
        => new(Microsoft.Extensions.Options.Options.Create(options), Substitute.For<ILogger<SmtpEmailSender>>(), factory);

    private static SmtpOptions DefaultOptions() => new()
    {
        Host = "smtp.example.com",
        Port = 587,
        DefaultFrom = "default@example.com",
        MaxRetryAttempts = 3,
    };

    private static ISmtpClient FakeClient()
    {
        var client = Substitute.For<ISmtpClient>();
        return client;
    }

    [Fact]
    public async Task SendAsync_HappyPath_ConnectsAuthenticatesSendsAndDisconnects()
    {
        var options = DefaultOptions();
        options.Username = "user";
        options.Password = "pass";
        var client = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        var sender = CreateSender(options, factory);

        await sender.SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        await client.Received(1).ConnectAsync("smtp.example.com", 587, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>());
        await client.Received(1).AuthenticateAsync("user", "pass", Arg.Any<CancellationToken>());
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
        await client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UsernameNotSet_SkipsAuthenticate()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        await client.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UsernameSetWithNullPassword_AuthenticatesWithEmptyPassword()
    {
        var options = DefaultOptions();
        options.Username = "user";
        options.Password = null;
        var client = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        await client.Received(1).AuthenticateAsync("user", string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UseStartTlsFalse_PassesAutoSecureSocketOptions()
    {
        var options = DefaultOptions();
        options.UseStartTls = false;
        var client = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        await client.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), SecureSocketOptions.Auto, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_FromNotSet_UsesDefaultFrom()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.From.Mailboxes.Single().Address.ShouldBe("default@example.com");
    }

    [Fact]
    public async Task SendAsync_FromSet_UsesMessageFrom()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "Body", From: "override@example.com"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.From.Mailboxes.Single().Address.ShouldBe("override@example.com");
    }

    [Fact]
    public async Task SendAsync_CcAndBccNull_NoEntriesAdded()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Cc.ShouldBeEmpty();
        captured.Bcc.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_CcAndBccPopulated_EntriesAdded()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "Body", Cc: ["cc@example.com"], Bcc: ["bcc@example.com"]),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Cc.Mailboxes.Single().Address.ShouldBe("cc@example.com");
        captured.Bcc.Mailboxes.Single().Address.ShouldBe("bcc@example.com");
    }

    [Fact]
    public async Task SendAsync_IsBodyHtmlTrue_SetsHtmlBody()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "<p>hi</p>", IsBodyHtml: true),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.HtmlBody.ShouldBe("<p>hi</p>");
        captured.TextBody.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_IsBodyHtmlFalse_SetsTextBody()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "plain text", IsBodyHtml: false),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.TextBody.ShouldBe("plain text");
    }

    [Fact]
    public async Task SendAsync_IsBodyHtmlTrueWithPlainTextBody_SetsBothHtmlAndTextBody()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "<p>hi</p>", IsBodyHtml: true, PlainTextBody: "hi"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.HtmlBody.ShouldBe("<p>hi</p>");
        captured.TextBody.ShouldBe("hi");
    }

    [Fact]
    public async Task SendAsync_IsBodyHtmlFalseWithPlainTextBody_PlainTextBodyIgnored()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("to@example.com", "Subject", "plain text", IsBodyHtml: false, PlainTextBody: "should be ignored"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.TextBody.ShouldBe("plain text");
        captured.HtmlBody.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_MultipleCommaSeparatedToAddresses_AddsAllRecipients()
    {
        var options = DefaultOptions();
        var client = FakeClient();
        MimeMessage? captured = null;
        _ = client.SendAsync(Arg.Do<MimeMessage>(m => captured = m), Arg.Any<CancellationToken>());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(
            new EmailMessage("first@example.com,second@example.com", "Subject", "Body"),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.To.Mailboxes.Select(m => m.Address).ShouldBe(["first@example.com", "second@example.com"]);
    }

    [Fact]
    public async Task SendAsync_MalformedToAddress_ThrowsImmediatelyWithoutCreatingClient()
    {
        var options = DefaultOptions();
        var factory = Substitute.For<ISmtpClientFactory>();

        await Should.ThrowAsync<ParseException>(() =>
            CreateSender(options, factory).SendAsync(new EmailMessage(string.Empty, "Subject", "Body"), TestContext.Current.CancellationToken));

        factory.DidNotReceive().Create();
    }

    [Fact]
    public async Task SendAsync_TransientFailureThenSuccess_Retries()
    {
        var options = DefaultOptions();
        options.MaxRetryAttempts = 2;
        var failingClient = FakeClient();
        failingClient.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("connect failed"));
        var succeedingClient = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(failingClient, succeedingClient);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        await succeedingClient.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_AllAttemptsFail_LastExceptionPropagates()
    {
        var options = DefaultOptions();
        options.MaxRetryAttempts = 2;
        var client = FakeClient();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("connect failed"));
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        var ex = await Should.ThrowAsync<IOException>(() =>
            CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("connect failed");
        factory.Received(2).Create();
    }

    [Fact]
    public async Task SendAsync_OperationCanceledException_NotRetried()
    {
        var options = DefaultOptions();
        options.MaxRetryAttempts = 3;
        var client = FakeClient();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await Should.ThrowAsync<OperationCanceledException>(() =>
            CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken));

        factory.Received(1).Create();
    }

    [Fact]
    public async Task SendAsync_MaxRetryAttemptsZero_ClampedToAtLeastOneAttempt()
    {
        var options = DefaultOptions();
        options.MaxRetryAttempts = 0;
        var client = FakeClient();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);

        await CreateSender(options, factory).SendAsync(new EmailMessage("to@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        factory.Received(1).Create();
    }
}
