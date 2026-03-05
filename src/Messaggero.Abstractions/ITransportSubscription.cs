namespace Messaggero.Abstractions;

/// <summary>
/// Transport-level subscription handle. Used internally by the core orchestrator.
/// </summary>
public interface ITransportSubscription : IAsyncDisposable
{
    /// <summary>
    /// Whether the transport subscription is currently active.
    /// </summary>
    bool IsActive { get; }
}
