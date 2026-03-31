namespace Messaggero.Abstractions;

/// <summary>
/// Pluggable serialization interface. Each adapter owns its serializer.
/// Default implementation uses System.Text.Json.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>Serializes a message to bytes.</summary>
    byte[] Serialize<TMessage>(TMessage message) where TMessage : class;

    /// <summary>Deserializes bytes to a message.</summary>
    TMessage Deserialize<TMessage>(ReadOnlySpan<byte> data) where TMessage : class;

    /// <summary>The content type this serializer produces (e.g., "application/json").</summary>
    string ContentType { get; }
}
