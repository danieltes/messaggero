using Messaggero.Abstractions;
using Messaggero.Routing;

namespace Messaggero.Configuration;

/// <summary>
/// Immutable snapshot produced by <see cref="MessagingBuilder.Build()"/>.
/// </summary>
public sealed record MessagingConfiguration
{
    /// <summary>All registered transports.</summary>
    public required IReadOnlyDictionary<string, TransportRegistration> Transports { get; init; }

    /// <summary>Routing rules.</summary>
    public required RoutingTable RoutingTable { get; init; }

    /// <summary>All handler registrations.</summary>
    public required IReadOnlyList<HandlerRegistration> Handlers { get; init; }

    /// <summary>Default serializer (JSON).</summary>
    public required IMessageSerializer DefaultSerializer { get; init; }

    /// <summary>Whether structured logging/metrics/tracing is active.</summary>
    public bool ObservabilityEnabled { get; init; }
}
