using Confluent.Kafka;
using Messaggero.Abstractions;
using Microsoft.Extensions.Logging;

using MessageMetadata = Messaggero.Abstractions.MessageMetadata;

namespace Messaggero.Transport.Kafka;

/// <summary>
/// Manages a single Kafka subscription with a dedicated consumer loop.
/// Each subscription has its own IConsumer (NOT thread-safe).
/// </summary>
internal sealed class KafkaSubscription : ITransportSubscription
{
    private readonly ConsumerConfig _config;
    private readonly string _topic;
    private readonly Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> _handler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private IConsumer<string?, byte[]>? _consumer;
    private Task? _consumeLoop;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaSubscription"/> class.
    /// </summary>
    public KafkaSubscription(
        ConsumerConfig config,
        string topic,
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
        ILogger logger)
    {
        _config = config;
        _topic = topic;
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsActive => !_disposed && _consumer is not null;

    /// <summary>
    /// Starts the consumer loop on a background task.
    /// </summary>
    public void Start(CancellationToken externalToken)
    {
        _consumer = new ConsumerBuilder<string?, byte[]>(_config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogWarning("Kafka consumer error on {Topic}: {Reason} (Code: {Code})",
                    _topic, error.Reason, error.Code);
            })
            .Build();

        _consumer.Subscribe(_topic);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        _consumeLoop = Task.Factory.StartNew(
            () => ConsumeLoopAsync(linkedCts.Token),
            linkedCts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await _cts.CancelAsync().ConfigureAwait(false);

        if (_consumeLoop is not null)
        {
            try
            {
                await _consumeLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _consumer?.Close();
        _consumer?.Dispose();
        _cts.Dispose();
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting consume loop for topic {Topic}", _topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer!.Consume(TimeSpan.FromMilliseconds(100));
                if (result is null)
                    continue;

                var metadata = ExtractMetadata(result);

                await _handler(
                    new ReadOnlyMemory<byte>(result.Message.Value),
                    metadata,
                    cancellationToken).ConfigureAwait(false);

                // Manual offset commit after successful processing
                _consumer.StoreOffset(result);
                _consumer.Commit(result);

                _logger.LogDebug("Processed message from {Topic} partition {Partition} offset {Offset}",
                    _topic, result.Partition.Value, result.Offset.Value);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming from {Topic}: {Reason}", _topic, ex.Error.Reason);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in consume loop for {Topic}", _topic);
            }
        }

        _logger.LogDebug("Consume loop ended for topic {Topic}", _topic);
    }

    private static MessageMetadata ExtractMetadata(ConsumeResult<string?, byte[]> result)
    {
        var headers = new Dictionary<string, string>();
        string messageId = string.Empty;
        string contentType = "application/json";
        string? correlationId = null;

        if (result.Message.Headers is not null)
        {
            foreach (var header in result.Message.Headers)
            {
                var value = System.Text.Encoding.UTF8.GetString(header.GetValueBytes());
                switch (header.Key)
                {
                    case "x-message-id":
                        messageId = value;
                        break;
                    case "x-content-type":
                        contentType = value;
                        break;
                    case "x-correlation-id":
                        correlationId = value;
                        break;
                    default:
                        headers[header.Key] = value;
                        break;
                }
            }
        }

        return new MessageMetadata
        {
            MessageId = messageId,
            RoutingKey = result.Message.Key,
            Headers = headers,
            Timestamp = result.Message.Timestamp.UtcDateTime,
            ContentType = contentType,
            CorrelationId = correlationId
        };
    }
}
