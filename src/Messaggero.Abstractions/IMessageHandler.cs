namespace Messaggero.Abstractions;

/// <summary>
/// Implemented by consumers to process incoming messages.
/// </summary>
/// <typeparam name="T">The type of the message payload.</typeparam>
public interface IMessageHandler<T>
{
    /// <summary>
    /// Handles an incoming message envelope.
    /// </summary>
    /// <param name="envelope">The message envelope containing the deserialized payload and metadata.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the message has been processed.</returns>
    Task HandleAsync(
        MessageEnvelope<T> envelope,
        CancellationToken cancellationToken = default);
}
