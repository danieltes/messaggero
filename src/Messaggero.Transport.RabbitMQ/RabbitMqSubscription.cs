using Messaggero.Abstractions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaggero.Transport.RabbitMQ;

/// <summary>
/// Manages a single RabbitMQ subscription (one channel, one consumer).
/// </summary>
internal sealed class RabbitMqSubscription : ITransportSubscription
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly Func<ReadOnlyMemory<byte>, Abstractions.MessageMetadata, CancellationToken, Task> _handler;
    private readonly ILogger _logger;
    private string? _consumerTag;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqSubscription"/> class.
    /// </summary>
    public RabbitMqSubscription(
        IChannel channel,
        string queueName,
        Func<ReadOnlyMemory<byte>, Abstractions.MessageMetadata, CancellationToken, Task> handler,
        ILogger logger)
    {
        _channel = channel;
        _queueName = queueName;
        _handler = handler;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsActive => !_disposed && _channel.IsOpen;

    /// <summary>
    /// Starts consuming messages from the queue.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var metadata = new Abstractions.MessageMetadata
                {
                    MessageId = ea.BasicProperties.MessageId ?? string.Empty,
                    RoutingKey = string.IsNullOrEmpty(ea.RoutingKey) ? null : ea.RoutingKey,
                    ContentType = ea.BasicProperties.ContentType ?? "application/json",
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime),
                    CorrelationId = string.IsNullOrEmpty(ea.BasicProperties.CorrelationId) ? null : ea.BasicProperties.CorrelationId,
                    Headers = ExtractHeaders(ea.BasicProperties.Headers)
                };

                await _handler(ea.Body, metadata, cancellationToken).ConfigureAwait(false);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId} from {Queue}",
                    ea.BasicProperties.MessageId, _queueName);
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        };

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_consumerTag is not null && _channel.IsOpen)
        {
            try
            {
                await _channel.BasicCancelAsync(_consumerTag).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cancelling consumer {ConsumerTag}", _consumerTag);
            }
        }

        if (_channel.IsOpen)
        {
            await _channel.CloseAsync().ConfigureAwait(false);
        }

        _channel.Dispose();
    }

    private static Dictionary<string, string> ExtractHeaders(IDictionary<string, object?>? headers)
    {
        var result = new Dictionary<string, string>();
        if (headers is null) return result;

        foreach (var kvp in headers)
        {
            if (kvp.Value is byte[] bytes)
                result[kvp.Key] = System.Text.Encoding.UTF8.GetString(bytes);
            else if (kvp.Value is not null)
                result[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
        }

        return result;
    }
}
