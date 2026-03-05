namespace Messaggero.Abstractions;

/// <summary>
/// Top-level facade combining publish, subscribe, and lifecycle management.
/// Registered as a singleton in DI. Consumers interact with this interface only.
/// </summary>
public interface IMessageBus : IAsyncDisposable
{
    /// <summary>
    /// Publishes a message to the specified destination.
    /// </summary>
    /// <typeparam name="T">The type of the message payload.</typeparam>
    /// <param name="destination">The target destination name.</param>
    /// <param name="payload">The message payload to publish.</param>
    /// <param name="options">Optional publish options (routing key, headers, correlation ID).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PublishAsync<T>(
        string destination,
        T payload,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages on the specified destination.
    /// </summary>
    /// <typeparam name="T">The type of the message payload.</typeparam>
    /// <param name="destination">The source destination name.</param>
    /// <param name="groupId">The consumer group identifier.</param>
    /// <param name="handler">The message handler to process incoming messages.</param>
    /// <param name="options">Optional subscription options (concurrency, error strategy).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A handle that can be disposed to unsubscribe.</returns>
    Task<ISubscriptionHandle> SubscribeAsync<T>(
        string destination,
        string groupId,
        IMessageHandler<T> handler,
        SubscriptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the health of the active transport connection.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A health check result indicating the connection status.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a listener for lifecycle events.
    /// </summary>
    /// <param name="listener">The callback to invoke when a lifecycle event occurs.</param>
    /// <returns>A disposable that, when disposed, removes the listener.</returns>
    IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener);
}
