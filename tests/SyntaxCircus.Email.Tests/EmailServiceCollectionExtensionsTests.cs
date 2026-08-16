namespace SyntaxCircus.Email.Tests;

public class EmailServiceCollectionExtensionsTests
{
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
