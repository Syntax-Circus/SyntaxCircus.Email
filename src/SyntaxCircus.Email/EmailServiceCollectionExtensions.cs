using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.Email;

/// <summary>
/// Registers the package's email sender implementations with dependency injection.
/// </summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SmtpEmailSender"/> as the singleton <see cref="IEmailSender"/>.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">
    /// The configuration whose <see cref="SmtpOptions.SectionName"/> section supplies static SMTP options.
    /// </param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// A previously registered <see cref="ISmtpOptionsProvider"/> or <see cref="ISmtpClientFactory"/>
    /// is retained. Register custom implementations before calling this method.
    /// </remarks>
    public static IServiceCollection AddSmtpEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.TryAddSingleton<ISmtpOptionsProvider, StaticSmtpOptionsProvider>();
        services.TryAddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddSingleton<IEmailSender>(sp => new SmtpEmailSender(
            sp.GetRequiredService<ISmtpOptionsProvider>(),
            sp.GetRequiredService<ILogger<SmtpEmailSender>>(),
            sp.GetRequiredService<ISmtpClientFactory>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="NullEmailSender"/> as the singleton <see cref="IEmailSender"/>.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>The registered sender logs recipient and subject but never delivers email.</remarks>
    public static IServiceCollection AddNullEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IEmailSender, NullEmailSender>();
        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="InMemoryEmailSender"/> as both itself and <see cref="IEmailSender"/>.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>Resolve <see cref="InMemoryEmailSender"/> directly to inspect captured messages.</remarks>
    public static IServiceCollection AddInMemoryEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InMemoryEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
        return services;
    }
}
