using Messaggero.Abstractions;

namespace Messaggero.Configuration;

/// <summary>
/// Internal model for a registered transport adapter.
/// </summary>
public sealed class TransportRegistration
{
    /// <summary>Unique name identifying this transport.</summary>
    public required string Name { get; init; }

    /// <summary>Factory for creating the adapter instance.</summary>
    public required Func<IServiceProvider, ITransportAdapter> AdapterFactory { get; init; }

    /// <summary>Transport-specific options.</summary>
    public required TransportOptions Options { get; init; }

    /// <summary>Override serializer for this transport. Null = default JSON.</summary>
    public IMessageSerializer? Serializer { get; init; }
}
