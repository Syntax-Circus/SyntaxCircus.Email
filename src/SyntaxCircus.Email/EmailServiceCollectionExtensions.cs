using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.TryAddSingleton<ISmtpClientFactory, MailKitSmtpClientFactory>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    public static IServiceCollection AddNullEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IEmailSender, NullEmailSender>();
        return services;
    }

    public static IServiceCollection AddInMemoryEmailSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InMemoryEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<InMemoryEmailSender>());
        return services;
    }
}
