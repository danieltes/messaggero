using Messaggero.Abstractions;
using Messaggero.Model;

namespace Messaggero.Testing;

/// <summary>
/// Test double for <see cref="IMessageBus"/> with assertion helpers.
/// </summary>
public sealed class TestMessageBus : IMessageBus
{
    private readonly List<(string Type, object Message, MessageHeaders Headers)> _published = [];
    private readonly Lock _lock = new();

    public Task<PublishResult> PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        return PublishAsync(message, new MessageHeaders(), cancellationToken);
    }

    public Task<PublishResult> PublishAsync<TMessage>(TMessage message, MessageHeaders headers, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        lock (_lock)
        {
            _published.Add((typeof(TMessage).Name, message, headers));
        }

        return Task.FromResult(new PublishResult
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Outcomes =
            [
                new TransportOutcome
                {
                    TransportName = "test",
                    Success = true
                }
            ]
        });
    }

    /// <summary>Asserts that at least one message of the given type was published.</summary>
    public void AssertPublished<TMessage>() where TMessage : class
    {
        lock (_lock)
        {
            if (!_published.Any(p => p.Type == typeof(TMessage).Name))
                throw new InvalidOperationException($"No message of type {typeof(TMessage).Name} was published.");
        }
    }

    /// <summary>Returns all published messages of a specific type.</summary>
    public IReadOnlyList<TMessage> GetPublishedMessages<TMessage>() where TMessage : class
    {
        lock (_lock)
        {
            return _published
                .Where(p => p.Type == typeof(TMessage).Name)
                .Select(p => (TMessage)p.Message)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>Returns all published messages.</summary>
    public IReadOnlyList<(string Type, object Message, MessageHeaders Headers)> GetAllPublished()
    {
        lock (_lock)
        {
            return _published.ToList().AsReadOnly();
        }
    }

    /// <summary>Clears all captured messages.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _published.Clear();
        }
    }
}
