using Messaggero.Abstractions;
using Messaggero.Routing;
using Messaggero.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Messaggero;

/// <summary>
/// Extension methods for registering the Messaggero message bus with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Messaggero message bus to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure the message bus builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessaggero(
        this IServiceCollection services,
        Action<MessageBusBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MessageBusBuilder();
        configure(builder);
        builder.Validate();

        // Apply transport-registered services
        foreach (var action in builder.ServiceActions)
        {
            action(services);
        }

        var serializer = builder.Serializer ?? new JsonMessageSerializer();
        services.AddSingleton<IMessageSerializer>(serializer);

        // Build the transport router
        var router = builder.BuildRouter();

        services.AddSingleton<IMessageBus>(sp =>
        {
            var ser = sp.GetRequiredService<IMessageSerializer>();
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<MessageBus>();
            return new MessageBus(router, ser, logger);
        });

        return services;
    }
}
