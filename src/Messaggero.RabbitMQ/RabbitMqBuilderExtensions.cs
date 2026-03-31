using Messaggero.Configuration;

namespace Messaggero.RabbitMQ;

/// <summary>
/// Extension methods for adding RabbitMQ transport to <see cref="MessagingBuilder"/>.
/// </summary>
public static class RabbitMqBuilderExtensions
{
    /// <summary>Registers a RabbitMQ transport adapter.</summary>
    public static MessagingBuilder AddRabbitMQ(
        this MessagingBuilder builder,
        string name,
        Action<RabbitMqOptions> configure)
    {
        var options = new RabbitMqOptions();
        configure(options);

        builder.AddTransport(
            name,
            sp => new RabbitMqTransportAdapter(name, options),
            opts =>
            {
                opts.RetryPolicy = options.RetryPolicy;
                opts.PrefetchCount = options.PrefetchCount;
            });

        return builder;
    }
}
