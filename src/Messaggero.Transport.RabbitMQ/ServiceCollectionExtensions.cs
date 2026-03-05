using Messaggero.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Messaggero.Transport.RabbitMQ;

/// <summary>
/// Extension methods for registering the RabbitMQ transport with the message bus.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the message bus to use RabbitMQ as the sole transport (backward compatible).
    /// Sets RabbitMQ as the default transport.
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="configure">An action to configure the RabbitMQ connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder UseRabbitMq(
        this MessageBusBuilder builder,
        Action<RabbitMqConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var config = new RabbitMqConfiguration();
        configure(config);
        config.Validate();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(config);
        });

        builder.UseTransport(CreateTransport(config));
        return builder;
    }

    /// <summary>
    /// Registers RabbitMQ as a named transport using the default name "RabbitMQ".
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="configure">An action to configure the RabbitMQ connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder AddRabbitMq(
        this MessageBusBuilder builder,
        Action<RabbitMqConfiguration> configure)
    {
        return AddRabbitMq(builder, "RabbitMQ", configure);
    }

    /// <summary>
    /// Registers RabbitMQ as a named transport with the specified name.
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="name">The name to register the transport under.</param>
    /// <param name="configure">An action to configure the RabbitMQ connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder AddRabbitMq(
        this MessageBusBuilder builder,
        string name,
        Action<RabbitMqConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var config = new RabbitMqConfiguration();
        configure(config);
        config.Validate();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(config);
        });

        builder.AddTransport(name, CreateTransport(config));
        return builder;
    }

    private static RabbitMqTransport CreateTransport(RabbitMqConfiguration config)
    {
        return new RabbitMqTransport(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<RabbitMqTransport>.Instance);
    }
}
