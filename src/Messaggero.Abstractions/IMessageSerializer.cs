namespace Messaggero.Abstractions;

/// <summary>
/// Pluggable serialization contract for message payloads.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// The content type this serializer produces (e.g., "application/json").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes a value to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The serialized byte array.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserializes a byte span to a value.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="data">The byte span to deserialize.</param>
    /// <returns>The deserialized value.</returns>
    T Deserialize<T>(ReadOnlySpan<byte> data);
}
