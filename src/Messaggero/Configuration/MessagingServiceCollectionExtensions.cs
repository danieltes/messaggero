using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding Messaggero to <see cref="IServiceCollection"/>.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Configures and registers the Messaggero messaging infrastructure.
    /// </summary>
    public static IServiceCollection AddMessaggero(
        this IServiceCollection services,
        Action<MessagingBuilder> configure)
    {
        var builder = new MessagingBuilder();
        configure(builder);
        var config = builder.Build();

        services.AddSingleton(config);

        // Register handler types in DI
        foreach (var handler in config.Handlers)
        {
            services.AddTransient(handler.HandlerType);
        }

        services.AddSingleton<MessagingHost>(sp =>
        {
            var host = new MessagingHost(
                config,
                sp,
                sp.GetRequiredService<ILogger<MessagingHost>>());
            return host;
        });

        services.AddSingleton<IMessageBus>(sp =>
        {
            var host = sp.GetRequiredService<MessagingHost>();
            return new MessageBus(
                config,
                host.Adapters,
                sp.GetRequiredService<ILogger<MessageBus>>());
        });

        services.AddHostedService(sp => sp.GetRequiredService<MessagingHost>());

        return services;
    }
}
