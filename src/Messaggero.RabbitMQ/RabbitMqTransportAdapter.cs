using System.Text;
using Messaggero.Abstractions;
using Messaggero.Errors;
using Messaggero.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaggero.RabbitMQ;

/// <summary>
/// RabbitMQ transport adapter using RabbitMQ.Client v7+.
/// </summary>
public sealed class RabbitMqTransportAdapter : ITransportAdapter
{
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private IChannel? _publishChannel;
    private readonly List<(IChannel Channel, string ConsumerTag, CancellationTokenSource Cts)> _consumers = [];
    private readonly Lock _lock = new();

    public RabbitMqTransportAdapter(string name, RabbitMqOptions options)
    {
        Name = name;
        _options = options;
    }

    public string Name { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = _options.AutomaticRecoveryEnabled
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _publishChannel = await _connection.CreateChannelAsync(channelOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var (channel, consumerTag, cts) in _consumers)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _consumers.Clear();
        }

        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _publishChannel.Dispose();
            _publishChannel = null;
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _connection.Dispose();
            _connection = null;
        }
    }

    public async Task<TransportOutcome> PublishAsync(Message message, Destination destination, CancellationToken cancellationToken)
    {
        if (_publishChannel is null)
            return new TransportOutcome
            {
                TransportName = Name,
                Success = false,
                Error = new PublishFailure(Name, "RabbitMQ channel not started.")
            };

        try
        {
            var routingKey = destination.TransportOverrides?.GetValueOrDefault("routingKey") ?? destination.Name;
            var properties = new BasicProperties
            {
                MessageId = message.Id,
                ContentType = message.Headers.ContentType ?? "application/octet-stream",
                Persistent = true,
                Headers = new Dictionary<string, object?>()
            };

            foreach (var (key, value) in message.Headers)
            {
                properties.Headers[key] = Encoding.UTF8.GetBytes(value);
            }

            await _publishChannel.BasicPublishAsync(
                exchange: destination.Name,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: message.Payload,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // In v7, BasicPublishAsync waits for broker confirm when publisher confirmations are enabled

            return new TransportOutcome
            {
                TransportName = Name,
                Success = true,
                BrokerMetadata = new Dictionary<string, string>
                {
                    ["exchange"] = destination.Name,
                    ["routingKey"] = routingKey
                }
            };
        }
        catch (Exception ex)
        {
            return new TransportOutcome
            {
                TransportName = Name,
                Success = false,
                Error = new PublishFailure(Name, $"RabbitMQ publish failed: {ex.Message}", ex)
            };
        }
    }

    public async Task SubscribeAsync(Destination destination, Func<Message, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
    {
        if (_connection is null)
            throw new InvalidOperationException("RabbitMQ connection not started.");

        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.BasicQosAsync(0, (ushort)_options.PrefetchCount, false, cancellationToken).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var headers = new MessageHeaders();
            if (ea.BasicProperties.Headers is not null)
            {
                foreach (var (key, value) in ea.BasicProperties.Headers)
                {
                    if (value is byte[] bytes)
                        headers.Set(key, Encoding.UTF8.GetString(bytes));
                    else if (value is not null)
                        headers.Set(key, value.ToString()!);
                }
            }

            headers.ContentType ??= ea.BasicProperties.ContentType;

            var message = new Message
            {
                Id = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString("N"),
                Type = headers.TryGetValue("message-type", out var mt) ? mt : destination.Name,
                Payload = ea.Body.ToArray(),
                Headers = headers,
                Timestamp = DateTimeOffset.UtcNow,
                SourceTransport = Name
            };

            // Store delivery tag for ack/nack
            headers.Set("rabbitmq-delivery-tag", ea.DeliveryTag.ToString());
            headers.Set("rabbitmq-channel-id", channel.GetHashCode().ToString());

            await onMessage(message, cts.Token).ConfigureAwait(false);
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: destination.Name,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _consumers.Add((channel, consumerTag, cts));
        }
    }

    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Headers.TryGetValue("rabbitmq-delivery-tag", out var tagStr)
            && ulong.TryParse(tagStr, out var deliveryTag)
            && message.Headers.TryGetValue("rabbitmq-channel-id", out var channelIdStr))
        {
            lock (_lock)
            {
                foreach (var (channel, _, _) in _consumers)
                {
                    if (channel.GetHashCode().ToString() == channelIdStr)
                    {
                        channel.BasicAckAsync(deliveryTag, false, cancellationToken)
                            .AsTask().GetAwaiter().GetResult();
                        return;
                    }
                }
            }
        }
    }

    public async Task RejectAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.Headers.TryGetValue("rabbitmq-delivery-tag", out var tagStr)
            && ulong.TryParse(tagStr, out var deliveryTag)
            && message.Headers.TryGetValue("rabbitmq-channel-id", out var channelIdStr))
        {
            lock (_lock)
            {
                foreach (var (channel, _, _) in _consumers)
                {
                    if (channel.GetHashCode().ToString() == channelIdStr)
                    {
                        channel.BasicNackAsync(deliveryTag, false, false, cancellationToken)
                            .AsTask().GetAwaiter().GetResult();
                        return;
                    }
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
