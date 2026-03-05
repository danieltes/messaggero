namespace Messaggero.Abstractions;

/// <summary>
/// Handle returned from a subscription that can be used to unsubscribe.
/// </summary>
public interface ISubscriptionHandle : IAsyncDisposable
{
    /// <summary>
    /// The destination this subscription is listening on.
    /// </summary>
    string Destination { get; }

    /// <summary>
    /// The consumer group identifier for this subscription.
    /// </summary>
    string GroupId { get; }

    /// <summary>
    /// Whether the subscription is currently active and receiving messages.
    /// </summary>
    bool IsActive { get; }
}
