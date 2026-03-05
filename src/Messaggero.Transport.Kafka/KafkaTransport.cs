using System.Collections.Concurrent;
using Confluent.Kafka;
using Messaggero.Abstractions;
using Microsoft.Extensions.Logging;

using MessageMetadata = Messaggero.Abstractions.MessageMetadata;

namespace Messaggero.Transport.Kafka;

/// <summary>
/// Kafka transport implementation using a single thread-safe producer
/// and per-subscription consumers.
/// </summary>
public sealed class KafkaTransport : IMessageBusTransport
{
    private readonly KafkaConfiguration _config;
    private readonly ILogger<KafkaTransport> _logger;
    private readonly ConcurrentBag<Action<LifecycleEvent>> _listeners = [];
    private readonly ConcurrentBag<KafkaSubscription> _subscriptions = [];

    private IProducer<string?, byte[]>? _producer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaTransport"/> class.
    /// </summary>
    /// <param name="config">The Kafka configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public KafkaTransport(KafkaConfiguration config, ILogger<KafkaTransport> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config.Validate();
    }

    /// <inheritdoc />
    public string Name => "Kafka";

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _config.BootstrapServers,
            Acks = ParseAcks(_config.Acks),
            EnableIdempotence = _config.EnableIdempotence,
            CompressionType = ParseCompressionType(_config.CompressionType),
            BatchSize = _config.BatchSize,
            LingerMs = _config.LingerMs
        };

        _producer = new ProducerBuilder<string?, byte[]>(producerConfig)
            .SetErrorHandler((_, error) =>
            {
                var eventType = error.IsFatal
                    ? LifecycleEventType.TransportFailed
                    : LifecycleEventType.TransportReconnecting;
                EmitEvent(eventType);
                _logger.LogWarning("Kafka producer error: {Reason} (Code: {Code}, IsFatal: {IsFatal})",
                    error.Reason, error.Code, error.IsFatal);
            })
            .Build();

        EmitEvent(LifecycleEventType.TransportConnected);
        _logger.LogInformation("Kafka transport connected to {Servers}", _config.BootstrapServers);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
        {
            sub.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        if (_producer is not null)
        {
            try
            {
                _producer.Flush(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error flushing Kafka producer during disconnect");
            }

            _producer.Dispose();
            _producer = null;
        }

        EmitEvent(LifecycleEventType.TransportDisconnected);
        _logger.LogInformation("Kafka transport disconnected");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        MessageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_producer is null)
            throw new InvalidOperationException("Kafka transport is not connected. Call ConnectAsync first.");

        var message = new Message<string?, byte[]>
        {
            Key = metadata.RoutingKey,
            Value = body.ToArray(),
            Timestamp = new Timestamp(metadata.Timestamp),
            Headers = MapHeaders(metadata)
        };

        try
        {
            var result = await _producer.ProduceAsync(destination, message, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Published message {MessageId} to {Topic} partition {Partition} offset {Offset}",
                metadata.MessageId, destination, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string?, byte[]> ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId} to {Topic}", metadata.MessageId, destination);
            throw new InvalidOperationException($"Failed to publish to Kafka topic '{destination}': {ex.Error.Reason}", ex);
        }
    }

    /// <inheritdoc />
    public Task<ITransportSubscription> SubscribeAsync(
        string destination,
        string groupId,
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
        SubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config.BootstrapServers,
            GroupId = groupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = _config.SessionTimeoutMs,
            HeartbeatIntervalMs = _config.HeartbeatIntervalMs
        };

        var subscription = new KafkaSubscription(consumerConfig, destination, handler, _logger);
        subscription.Start(cancellationToken);

        _subscriptions.Add(subscription);

        _logger.LogInformation("Subscribed to {Topic} with group {GroupId}", destination, groupId);
        return Task.FromResult<ITransportSubscription>(subscription);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var isHealthy = _producer is not null;
        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = isHealthy,
            TransportName = Name,
            Description = isHealthy ? "Producer is active" : "Producer is not initialized"
        });
    }

    /// <inheritdoc />
    public IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener)
    {
        _listeners.Add(listener);
        return new ListenerDisposable();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await Task.Run(() => DisconnectAsync()).ConfigureAwait(false);
    }

    private void EmitEvent(LifecycleEventType type, string? destination = null, string? messageId = null, Exception? error = null)
    {
        var evt = new LifecycleEvent
        {
            EventType = type,
            TransportName = Name,
            Destination = destination,
            MessageId = messageId,
            Error = error
        };

        foreach (var listener in _listeners)
        {
            try
            {
                listener(evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lifecycle event listener threw an exception");
            }
        }
    }

    private static Headers MapHeaders(MessageMetadata metadata)
    {
        var headers = new Headers
        {
            { "x-message-id", System.Text.Encoding.UTF8.GetBytes(metadata.MessageId) },
            { "x-content-type", System.Text.Encoding.UTF8.GetBytes(metadata.ContentType) }
        };

        if (metadata.CorrelationId is not null)
            headers.Add("x-correlation-id", System.Text.Encoding.UTF8.GetBytes(metadata.CorrelationId));

        foreach (var kvp in metadata.Headers)
        {
            headers.Add(kvp.Key, System.Text.Encoding.UTF8.GetBytes(kvp.Value));
        }

        return headers;
    }

    private static Acks ParseAcks(string acks) => acks.ToLowerInvariant() switch
    {
        "all" or "-1" => Confluent.Kafka.Acks.All,
        "none" or "0" => Confluent.Kafka.Acks.None,
        "leader" or "1" => Confluent.Kafka.Acks.Leader,
        _ => Confluent.Kafka.Acks.All
    };

    private static CompressionType ParseCompressionType(string compression) => compression.ToLowerInvariant() switch
    {
        "lz4" => Confluent.Kafka.CompressionType.Lz4,
        "gzip" => Confluent.Kafka.CompressionType.Gzip,
        "snappy" => Confluent.Kafka.CompressionType.Snappy,
        "zstd" => Confluent.Kafka.CompressionType.Zstd,
        "none" => Confluent.Kafka.CompressionType.None,
        _ => Confluent.Kafka.CompressionType.None
    };

    private sealed class ListenerDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
