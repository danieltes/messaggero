using Messaggero.Configuration;

namespace Messaggero.Testing;

/// <summary>
/// Extension methods for adding the in-memory transport to <see cref="MessagingBuilder"/>.
/// </summary>
public static class InMemoryBuilderExtensions
{
    /// <summary>Registers an in-memory transport adapter for testing.</summary>
    public static MessagingBuilder AddInMemory(
        this MessagingBuilder builder,
        string name)
    {
        builder.AddTransport(
            name,
            _ => new InMemoryTransportAdapter(name));

        return builder;
    }
}
