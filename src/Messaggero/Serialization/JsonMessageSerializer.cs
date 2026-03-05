using System.Text.Json;
using Messaggero.Abstractions;

namespace Messaggero.Serialization;

/// <summary>
/// Default JSON message serializer using System.Text.Json.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance with default JSON serializer options.
    /// </summary>
    public JsonMessageSerializer()
        : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    /// <summary>
    /// Initializes a new instance with custom JSON serializer options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use.</param>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, _options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<T>(data, _options)
            ?? throw new JsonException($"Deserialization of {typeof(T).Name} returned null.");
    }
}
