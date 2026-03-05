namespace Messaggero.Abstractions;

/// <summary>
/// Transport implementation interface. Implemented by transport packages
/// (e.g., Messaggero.Transport.RabbitMQ). Not consumed directly by application code.
/// </summary>
public interface IMessageBusTransport : IAsyncDisposable
{
    /// <summary>
    /// The name of this transport (e.g., "RabbitMQ", "Kafka").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Establishes a connection to the message broker.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully disconnects from the message broker.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a serialized message to the specified destination.
    /// </summary>
    /// <param name="destination">The target destination (topic/exchange).</param>
    /// <param name="body">The serialized message body.</param>
    /// <param name="metadata">Wire-level metadata for the message.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PublishAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        MessageMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from the specified destination.
    /// </summary>
    /// <param name="destination">The source destination (topic/queue).</param>
    /// <param name="groupId">The consumer group identifier.</param>
    /// <param name="handler">A callback invoked for each received message.</param>
    /// <param name="options">Subscription configuration options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A subscription handle that can be disposed to unsubscribe.</returns>
    Task<ITransportSubscription> SubscribeAsync(
        string destination,
        string groupId,
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
        SubscriptionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of the transport connection.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A health check result indicating the connection status.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a listener for lifecycle events emitted by this transport.
    /// </summary>
    /// <param name="listener">The callback to invoke when a lifecycle event occurs.</param>
    /// <returns>A disposable that, when disposed, removes the listener.</returns>
    IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener);
}
