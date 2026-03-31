using Messaggero.Model;

namespace Messaggero.Abstractions;

/// <summary>
/// The primary publish surface for application code.
/// Resolves target transport(s) from the routing table based on message type.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message. The library resolves the target transport(s)
    /// from the routing table based on the message's type.
    /// </summary>
    Task<PublishResult> PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Publishes a message with explicit headers.
    /// </summary>
    Task<PublishResult> PublishAsync<TMessage>(
        TMessage message,
        MessageHeaders headers,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
