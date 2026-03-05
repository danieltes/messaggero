using Messaggero.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Messaggero.Transport.Kafka;

/// <summary>
/// Extension methods for registering the Kafka transport with the message bus.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the message bus to use Kafka as the sole transport (backward compatible).
    /// Sets Kafka as the default transport.
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="configure">An action to configure the Kafka connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder UseKafka(
        this MessageBusBuilder builder,
        Action<KafkaConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var config = new KafkaConfiguration();
        configure(config);
        config.Validate();

        var transport = new Lazy<KafkaTransport>(() => null!);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(config);
        });

        builder.UseTransport(CreateTransport(config, builder));
        return builder;
    }

    /// <summary>
    /// Registers Kafka as a named transport using the default name "Kafka".
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="configure">An action to configure the Kafka connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder AddKafka(
        this MessageBusBuilder builder,
        Action<KafkaConfiguration> configure)
    {
        return AddKafka(builder, "Kafka", configure);
    }

    /// <summary>
    /// Registers Kafka as a named transport with the specified name.
    /// </summary>
    /// <param name="builder">The message bus builder.</param>
    /// <param name="name">The name to register the transport under.</param>
    /// <param name="configure">An action to configure the Kafka connection settings.</param>
    /// <returns>The builder for chaining.</returns>
    public static MessageBusBuilder AddKafka(
        this MessageBusBuilder builder,
        string name,
        Action<KafkaConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var config = new KafkaConfiguration();
        configure(config);
        config.Validate();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(config);
        });

        builder.AddTransport(name, CreateTransport(config, builder));
        return builder;
    }

    private static KafkaTransport CreateTransport(KafkaConfiguration config, MessageBusBuilder builder)
    {
        // Create the transport immediately — logger will be null at this point
        // but KafkaTransport handles that gracefully
        return new KafkaTransport(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<KafkaTransport>.Instance);
    }
}
