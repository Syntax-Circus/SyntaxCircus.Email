namespace SyntaxCircus.Email.Tests;

public class EmailServiceCollectionExtensionsTests
{
    private sealed class FixedOptionsProvider(SmtpOptions options) : ISmtpOptionsProvider
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<SmtpOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(options);
        }
    }

    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void AddSmtpEmailSender_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            EmailServiceCollectionExtensions.AddSmtpEmailSender(null!, EmptyConfiguration()));
    }

    [Fact]
    public void AddSmtpEmailSender_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSmtpEmailSender(null!));
    }

    [Fact]
    public void AddSmtpEmailSender_ResolvesAsSmtpEmailSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmtpEmailSender(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEmailSender>().ShouldBeOfType<SmtpEmailSender>();
    }

    [Fact]
    public void AddSmtpEmailSender_RegistersMailKitSmtpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddSmtpEmailSender(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISmtpClientFactory>().ShouldBeOfType<MailKitSmtpClientFactory>();
    }

    [Fact]
    public void AddSmtpEmailSender_DoesNotOverrideAlreadyRegisteredSmtpClientFactory()
    {
        var services = new ServiceCollection();
        var customFactory = Substitute.For<ISmtpClientFactory>();
        services.AddSingleton(customFactory);

        services.AddSmtpEmailSender(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISmtpClientFactory>().ShouldBeSameAs(customFactory);
    }

    [Fact]
    public async Task AddSmtpEmailSender_DoesNotOverrideAlreadyRegisteredOptionsProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var customOptions = new SmtpOptions
        {
            Host = "dynamic.smtp.example.com",
            Port = 2525,
            DefaultFrom = "dynamic@example.com",
        };
        var customProvider = new FixedOptionsProvider(customOptions);
        var client = Substitute.For<MailKit.Net.Smtp.ISmtpClient>();
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);
        services.AddSingleton<ISmtpOptionsProvider>(customProvider);
        services.AddSingleton(factory);
        services.AddSmtpEmailSender(EmptyConfiguration());

        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage("to@example.com", "Subject", "Body"),
            TestContext.Current.CancellationToken);

        provider.GetRequiredService<ISmtpOptionsProvider>().ShouldBeSameAs(customProvider);
        customProvider.CallCount.ShouldBe(1);
        customProvider.LastCancellationToken.ShouldBe(TestContext.Current.CancellationToken);
        await client.Received(1).ConnectAsync(
            "dynamic.smtp.example.com",
            2525,
            MailKit.Security.SecureSocketOptions.StartTls,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AddNullEmailSender_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => EmailServiceCollectionExtensions.AddNullEmailSender(null!));
    }

    [Fact]
    public void AddNullEmailSender_ResolvesAsNullEmailSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNullEmailSender();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEmailSender>().ShouldBeOfType<NullEmailSender>();
    }

    [Fact]
    public void AddInMemoryEmailSender_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => EmailServiceCollectionExtensions.AddInMemoryEmailSender(null!));
    }

    [Fact]
    public void AddInMemoryEmailSender_IEmailSenderAndInMemoryEmailSender_ResolveToSameInstance()
    {
        var services = new ServiceCollection();
        services.AddInMemoryEmailSender();

        using var provider = services.BuildServiceProvider();

        var asInterface = provider.GetRequiredService<IEmailSender>();
        var asConcrete = provider.GetRequiredService<InMemoryEmailSender>();

        asInterface.ShouldBeSameAs(asConcrete);
    }
}
