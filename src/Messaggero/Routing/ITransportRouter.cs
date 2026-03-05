using Messaggero.Abstractions;

namespace Messaggero.Routing;

/// <summary>
/// Resolves the target transport for a given destination and optional message type.
/// </summary>
internal interface ITransportRouter
{
    /// <summary>
    /// Resolves the transport to use for the given destination and optional message type.
    /// </summary>
    /// <param name="destination">The target destination.</param>
    /// <param name="messageType">Optional CLR message type for type-based routing.</param>
    /// <returns>The resolved transport.</returns>
    /// <exception cref="InvalidOperationException">No matching rule and no default transport.</exception>
    IMessageBusTransport ResolveTransport(string destination, Type? messageType = null);

    /// <summary>
    /// All registered transports keyed by name.
    /// </summary>
    IReadOnlyDictionary<string, IMessageBusTransport> Transports { get; }
}
