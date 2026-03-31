using Messaggero.Model;

namespace Messaggero.Abstractions;

/// <summary>
/// Transport adapter contract that broker-specific packages implement.
/// </summary>
public interface ITransportAdapter : IAsyncDisposable
{
    /// <summary>Unique name identifying this adapter instance.</summary>
    string Name { get; }

    /// <summary>Starts the adapter (connections, consumers).</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Gracefully stops the adapter (drain, close).</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Publishes a serialized message to the broker.</summary>
    Task<TransportOutcome> PublishAsync(
        Message message,
        Destination destination,
        CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to messages for the given destination.
    /// The callback is invoked for each received message.
    /// </summary>
    Task SubscribeAsync(
        Destination destination,
        Func<Message, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken);

    /// <summary>Acknowledges successful processing of a message.</summary>
    Task AcknowledgeAsync(Message message, CancellationToken cancellationToken);

    /// <summary>Negatively acknowledges a message (trigger redelivery or DLQ).</summary>
    Task RejectAsync(Message message, CancellationToken cancellationToken);
}
