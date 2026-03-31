namespace Messaggero.Abstractions;

/// <summary>
/// Handler contract for consuming messages of a specific type.
/// </summary>
public interface IMessageHandler<in TMessage> where TMessage : class
{
    /// <summary>
    /// Processes a single message. Called by the library's dispatch loop.
    /// </summary>
    Task HandleAsync(
        TMessage message,
        MessageContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Optional lifecycle hooks for handler classes.
/// Implement this interface alongside <see cref="IMessageHandler{TMessage}"/>
/// to receive host start/stop notifications.
/// </summary>
public interface IHandlerLifecycle
{
    /// <summary>Called once when the messaging host starts.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Called once when the messaging host stops.</summary>
    Task DisposeAsync();
}
