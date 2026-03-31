using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Messaggero.Model;

/// <summary>
/// Wrapper around header key-value pairs with typed accessors.
/// </summary>
public sealed class MessageHeaders : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _headers;

    public MessageHeaders()
    {
        _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public MessageHeaders(IDictionary<string, string> headers)
    {
        _headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>MIME type of payload (e.g., "application/json"). Set by serializer.</summary>
    public string? ContentType
    {
        get => TryGetValue("content-type", out var v) ? v : null;
        set
        {
            if (value is not null)
                _headers["content-type"] = value;
            else
                _headers.Remove("content-type");
        }
    }

    /// <summary>Optional correlation identifier for tracing message chains.</summary>
    public string? CorrelationId
    {
        get => TryGetValue("correlation-id", out var v) ? v : null;
        set
        {
            if (value is not null)
                _headers["correlation-id"] = value;
            else
                _headers.Remove("correlation-id");
        }
    }

    public void Set(string key, string value) => _headers[key] = value;

    // IReadOnlyDictionary implementation
    public string this[string key] => _headers[key];
    public IEnumerable<string> Keys => _headers.Keys;
    public IEnumerable<string> Values => _headers.Values;
    public int Count => _headers.Count;
    public bool ContainsKey(string key) => _headers.ContainsKey(key);
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => _headers.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _headers.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
