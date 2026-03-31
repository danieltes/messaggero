using System.Collections.Concurrent;
using Messaggero.Abstractions;
using Messaggero.Model;

namespace Messaggero.Testing;

/// <summary>
/// In-memory transport adapter for testing without a running broker.
/// Satisfies the full <see cref="ITransportAdapter"/> contract.
/// </summary>
public sealed class InMemoryTransportAdapter : ITransportAdapter
{
    private readonly ConcurrentQueue<Message> _published = new();
    private readonly ConcurrentDictionary<string, List<Func<Message, CancellationToken, Task>>> _subscriptions = new();
    private readonly ConcurrentQueue<Message> _deadLetters = new();
    private readonly ConcurrentDictionary<string, Message> _pending = new();
    private bool _started;

    public InMemoryTransportAdapter(string name = "in-memory")
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>All published messages.</summary>
    public IReadOnlyCollection<Message> PublishedMessages => _published.ToArray();

    /// <summary>All dead-lettered messages.</summary>
    public IReadOnlyCollection<Message> DeadLetterMessages => _deadLetters.ToArray();

    /// <summary>All messages awaiting ack/nack.</summary>
    public IReadOnlyCollection<Message> PendingMessages => _pending.Values.ToArray();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _started = false;
        return Task.CompletedTask;
    }

    public async Task<TransportOutcome> PublishAsync(Message message, Destination destination, CancellationToken cancellationToken)
    {
        if (!_started)
            return new TransportOutcome
            {
                TransportName = Name,
                Success = false,
                Error = new Errors.PublishFailure(Name, "In-memory adapter not started.")
            };

        _published.Enqueue(message);

        // Deliver to subscribers
        if (_subscriptions.TryGetValue(destination.Name, out var handlers))
        {
            var enrichedMessage = new Message
            {
                Id = message.Id,
                Type = message.Type,
                Payload = message.Payload,
                Headers = message.Headers,
                Timestamp = message.Timestamp,
                SourceTransport = Name
            };

            _pending.TryAdd(message.Id, enrichedMessage);

            foreach (var handler in handlers)
            {
                await handler(enrichedMessage, cancellationToken).ConfigureAwait(false);
            }
        }

        return new TransportOutcome
        {
            TransportName = Name,
            Success = true,
            BrokerMetadata = new Dictionary<string, string>
            {
                ["queue-depth"] = _published.Count.ToString()
            }
        };
    }

    public Task SubscribeAsync(Destination destination, Func<Message, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
    {
        var handlers = _subscriptions.GetOrAdd(destination.Name, _ => []);
        lock (handlers)
        {
            handlers.Add(onMessage);
        }
        return Task.CompletedTask;
    }

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken)
    {
        _pending.TryRemove(message.Id, out _);
        return Task.CompletedTask;
    }

    public Task RejectAsync(Message message, CancellationToken cancellationToken)
    {
        if (_pending.TryRemove(message.Id, out var msg))
        {
            _deadLetters.Enqueue(msg);
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _started = false;
        return ValueTask.CompletedTask;
    }
}
