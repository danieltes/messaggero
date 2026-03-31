using System.Text.Json;
using Messaggero.Abstractions;
using Messaggero.Errors;

namespace Messaggero.Serialization;

/// <summary>
/// Default JSON serializer using System.Text.Json.
/// Tolerant reader: ignores unknown fields. AOT-friendly via source generators.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
    };

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public byte[] Serialize<TMessage>(TMessage message) where TMessage : class
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, Options);
    }

    /// <inheritdoc />
    public TMessage Deserialize<TMessage>(ReadOnlySpan<byte> data) where TMessage : class
    {
        try
        {
            return JsonSerializer.Deserialize<TMessage>(data, Options)
                ?? throw new DeserializationException($"Deserialization of {typeof(TMessage).Name} returned null.");
        }
        catch (JsonException ex)
        {
            throw new DeserializationException(
                $"Failed to deserialize message as {typeof(TMessage).Name}: {ex.Message}", ex);
        }
    }
}
