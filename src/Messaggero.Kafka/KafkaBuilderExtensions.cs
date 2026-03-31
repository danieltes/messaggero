using Messaggero.Configuration;

namespace Messaggero.Kafka;

/// <summary>
/// Extension methods for adding Kafka transport to <see cref="MessagingBuilder"/>.
/// </summary>
public static class KafkaBuilderExtensions
{
    /// <summary>Registers a Kafka transport adapter.</summary>
    public static MessagingBuilder AddKafka(
        this MessagingBuilder builder,
        string name,
        Action<KafkaOptions> configure)
    {
        var options = new KafkaOptions();
        configure(options);

        builder.AddTransport(
            name,
            sp => new KafkaTransportAdapter(name, options),
            opts =>
            {
                opts.RetryPolicy = options.RetryPolicy;
                opts.PrefetchCount = options.PrefetchCount;
            });

        return builder;
    }
}
