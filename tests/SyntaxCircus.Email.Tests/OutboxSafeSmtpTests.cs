using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SyntaxCircus.Email.Tests;

public class OutboxSafeSmtpTests
{
    private static SmtpOptions Options() => new()
    {
        Host = "smtp.example.com", DefaultFrom = "from@example.com", MaxRetryAttempts = 1,
        RetryMode = SmtpRetryMode.TransientOnly,
    };

    private static SmtpEmailSender Sender(SmtpOptions options, params ISmtpClient[] clients)
    {
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(clients[0], clients.Skip(1).ToArray());
        return new(Microsoft.Extensions.Options.Options.Create(options), Substitute.For<ILogger<SmtpEmailSender>>(), factory);
    }

    [Fact]
    public async Task StableMessageId_IsUsedOnEveryAttempt()
    {
        var options = Options();
        options.MaxRetryAttempts = 2;
        var first = Substitute.For<ISmtpClient>();
        MimeMessage? firstMessage = null;
        first.SendAsync(Arg.Do<MimeMessage>(message => firstMessage = message), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxBusy, "secret"));
        var second = Substitute.For<ISmtpClient>();
        MimeMessage? captured = null;
        _ = second.SendAsync(Arg.Do<MimeMessage>(message => captured = message), Arg.Any<CancellationToken>());
        await Sender(options, first, second).SendAsync(new("to@example.com", "subject", "body") { MessageId = "stable@example.com" }, TestContext.Current.CancellationToken);
        captured.ShouldNotBeNull();
        captured.MessageId.ShouldBe("stable@example.com");
        firstMessage.ShouldBeSameAs(captured);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedSend_CleanupFailureDoesNotFailOrResubmit(bool dispose)
    {
        var options = Options();
        options.MaxRetryAttempts = 2;
        var client = Substitute.For<ISmtpClient>();
        if (dispose) client.When(c => c.Dispose()).Do(_ => throw new IOException("secret"));
        else client.DisconnectAsync(true, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("secret"));
        await Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken);
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExplicitTlsMode_OverridesLegacyFlag()
    {
        var options = Options();
        options.TlsMode = SmtpTlsMode.SslOnConnect;
        var client = Substitute.For<ISmtpClient>();
        await Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken);
        await client.Received(1).ConnectAsync(options.Host, options.Port, SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PermanentRejection_IsSafeAndNotRetried()
    {
        var options = Options();
        options.MaxRetryAttempts = 3;
        var client = Substitute.For<ISmtpClient>();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(
            new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "password recipient subject body"));
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken));
        failure.Kind.ShouldBe(SmtpFailureKind.Permanent);
        failure.AcceptanceUncertain.ShouldBeFalse();
        failure.InnerException.ShouldBeNull();
        failure.ToString().ShouldNotContain("password");
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LostResponseDuringSend_IsUncertainAndNotRetried()
    {
        var options = Options();
        options.MaxRetryAttempts = 3;
        var client = Substitute.For<ISmtpClient>();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("password"));
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken));
        failure.AcceptanceUncertain.ShouldBeTrue();
        failure.Kind.ShouldBe(SmtpFailureKind.Transient);
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TotalTimeout_StopsConnectAndReturnsSafeFailure()
    {
        var options = Options();
        options.TotalSendTimeout = TimeSpan.FromMilliseconds(50);
        var client = Substitute.For<ISmtpClient>();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.InfiniteTimeSpan, call.ArgAt<CancellationToken>(3)));
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        failure.Kind.ShouldBe(SmtpFailureKind.Timeout);
        failure.AcceptanceUncertain.ShouldBeFalse();
    }

    [Fact]
    public async Task TotalTimeoutDuringSend_IsUncertain()
    {
        var options = Options();
        options.TotalSendTimeout = TimeSpan.FromMilliseconds(50);
        var client = Substitute.For<ISmtpClient>();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, call.ArgAt<CancellationToken>(1));
            return "accepted";
        });
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        failure.Kind.ShouldBe(SmtpFailureKind.Timeout);
        failure.AcceptanceUncertain.ShouldBeTrue();
    }

    [Fact]
    public async Task TotalTimeout_IncludesRetryDelay()
    {
        var options = Options();
        options.TotalSendTimeout = TimeSpan.FromMilliseconds(50);
        options.MaxRetryAttempts = 3;
        var client = Substitute.For<ISmtpClient>();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("secret"));
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        failure.Kind.ShouldBe(SmtpFailureKind.Timeout);
        failure.AcceptanceUncertain.ShouldBeFalse();
        await client.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CallerCancellationDuringSend_IsSafeAndUncertain()
    {
        var options = Options();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var client = Substitute.For<ISmtpClient>();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            source.Cancel();
            return Task.FromException<string>(new OperationCanceledException("password", source.Token));
        });
        OperationCanceledException? canceled = null;
        try { await Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), source.Token); }
        catch (OperationCanceledException exception) { canceled = exception; }
        var failure = canceled.ShouldBeOfType<SmtpDeliveryCanceledException>();
        failure.AcceptanceUncertain.ShouldBeTrue();
        failure.CancellationToken.ShouldBe(source.Token);
        failure.InnerException.ShouldBeNull();
        failure.ToString().ShouldNotContain("password");
    }

    [Fact]
    public async Task AuthenticationFailure_IsSafeAndNotRetried()
    {
        var options = Options();
        options.Username = "secret-user";
        options.MaxRetryAttempts = 3;
        var client = Substitute.For<ISmtpClient>();
        client.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuthenticationException("password"));
        var failure = await Should.ThrowAsync<SmtpDeliveryException>(() => Sender(options, client).SendAsync(new("to@example.com", "subject", "body"), TestContext.Current.CancellationToken));
        failure.Kind.ShouldBe(SmtpFailureKind.Authentication);
        failure.AcceptanceUncertain.ShouldBeFalse();
        await client.Received(1).AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyRetryLogs_DoNotIncludeRawExceptionOrMessageData()
    {
        var options = Options();
        options.RetryMode = SmtpRetryMode.Legacy;
        options.MaxRetryAttempts = 2;
        var first = Substitute.For<ISmtpClient>();
        first.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("password"));
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(first, Substitute.For<ISmtpClient>());
        var logger = new CaptureLogger();
        await new SmtpEmailSender(Microsoft.Extensions.Options.Options.Create(options), logger, factory)
            .SendAsync(new("private-recipient@example.com", "private-subject", "private-body"), TestContext.Current.CancellationToken);
        logger.Lines.ShouldNotBeEmpty();
        string.Join(" ", logger.Lines).ShouldNotContain("password");
        string.Join(" ", logger.Lines).ShouldNotContain("private-");
        logger.Exceptions.ShouldAllBe(exception => exception == null);
    }

    private sealed class CaptureLogger : ILogger<SmtpEmailSender>
    {
        public List<string> Lines { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    [Fact]
    public async Task CallerDeadline_CancelsOptionsRetrievalBeforeClientCreation()
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromMilliseconds(50));
        var factory = Substitute.For<ISmtpClientFactory>();
        var sender = new SmtpEmailSender(new WaitingOptionsProvider(), Substitute.For<ILogger<SmtpEmailSender>>(), factory);
        await Should.ThrowAsync<OperationCanceledException>(() => sender.SendAsync(new("to@example.com", "subject", "body"), budget.Token).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        factory.DidNotReceive().Create();
    }

    private sealed class WaitingOptionsProvider : ISmtpOptionsProvider
    {
        public async ValueTask<SmtpOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Options();
        }
    }
}
