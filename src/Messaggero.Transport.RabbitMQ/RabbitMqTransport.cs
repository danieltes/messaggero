using System.Collections.Concurrent;
using Messaggero.Abstractions;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Messaggero.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ transport implementation using two connections (publish/consume)
/// with channel pooling for publishers and auto-recovery.
/// </summary>
public sealed class RabbitMqTransport : IMessageBusTransport
{
    private readonly RabbitMqConfiguration _config;
    private readonly ILogger<RabbitMqTransport> _logger;
    private readonly ConcurrentBag<Action<LifecycleEvent>> _listeners = [];
    private readonly ConcurrentBag<RabbitMqSubscription> _subscriptions = [];
    private readonly SemaphoreSlim _channelPoolSemaphore;
    private readonly ConcurrentBag<IChannel> _publishChannels = [];

    private IConnection? _publishConnection;
    private IConnection? _consumeConnection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqTransport"/> class.
    /// </summary>
    /// <param name="config">The RabbitMQ configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public RabbitMqTransport(RabbitMqConfiguration config, ILogger<RabbitMqTransport> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config.Validate();
        _channelPoolSemaphore = new SemaphoreSlim(_config.PublishChannelPoolSize, _config.PublishChannelPoolSize);
    }

    /// <inheritdoc />
    public string Name => "RabbitMQ";

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var factory = CreateConnectionFactory();

        _publishConnection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        _consumeConnection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Wire lifecycle events
        _publishConnection.ConnectionShutdownAsync += (_, args) =>
        {
            EmitEvent(LifecycleEventType.TransportDisconnected, metadata: new Dictionary<string, object> { ["reason"] = args.ReplyText });
            return Task.CompletedTask;
        };

        _publishConnection.RecoverySucceededAsync += (_, _) =>
        {
            EmitEvent(LifecycleEventType.TransportConnected);
            return Task.CompletedTask;
        };

        _publishConnection.ConnectionRecoveryErrorAsync += (_, args) =>
        {
            EmitEvent(LifecycleEventType.TransportReconnecting, error: args.Exception);
            return Task.CompletedTask;
        };

        // Pre-create publish channels with publisher confirmations enabled
        for (var i = 0; i < _config.PublishChannelPoolSize; i++)
        {
            var channel = await CreatePublishChannelAsync(cancellationToken).ConfigureAwait(false);
            _publishChannels.Add(channel);
        }

        EmitEvent(LifecycleEventType.TransportConnected);
        _logger.LogInformation("RabbitMQ transport connected to {Host}:{Port}", _config.HostName, _config.Port);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
        {
            await sub.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var channel in _publishChannels)
        {
            await channel.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            channel.Dispose();
        }

        if (_consumeConnection is not null)
        {
            await _consumeConnection.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _consumeConnection.Dispose();
        }

        if (_publishConnection is not null)
        {
            await _publishConnection.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _publishConnection.Dispose();
        }

        EmitEvent(LifecycleEventType.TransportDisconnected);
        _logger.LogInformation("RabbitMQ transport disconnected");
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        Abstractions.MessageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_publishConnection is null || !_publishConnection.IsOpen)
            throw new InvalidOperationException("RabbitMQ transport is not connected. Call ConnectAsync first.");

        var channel = await AcquirePublishChannelAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Ensure exchange exists (fanout, durable)
            await channel.ExchangeDeclareAsync(
                exchange: destination,
                type: ExchangeType.Fanout,
                durable: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var properties = new BasicProperties
            {
                MessageId = metadata.MessageId,
                ContentType = metadata.ContentType,
                Timestamp = new AmqpTimestamp(metadata.Timestamp.ToUnixTimeSeconds()),
                Persistent = true,
                CorrelationId = metadata.CorrelationId ?? string.Empty
            };

            // Map headers
            if (metadata.Headers.Count > 0)
            {
                properties.Headers = new Dictionary<string, object?>();
                foreach (var kvp in metadata.Headers)
                {
                    properties.Headers[kvp.Key] = kvp.Value;
                }
            }

            await channel.BasicPublishAsync(
                exchange: destination,
                routingKey: metadata.RoutingKey ?? string.Empty,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Published message {MessageId} to exchange {Exchange}", metadata.MessageId, destination);
        }
        finally
        {
            ReleasePublishChannel(channel);
        }
    }

    /// <inheritdoc />
    public async Task<ITransportSubscription> SubscribeAsync(
        string destination,
        string groupId,
        Func<ReadOnlyMemory<byte>, Abstractions.MessageMetadata, CancellationToken, Task> handler,
        SubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_consumeConnection is null || !_consumeConnection.IsOpen)
            throw new InvalidOperationException("RabbitMQ transport is not connected. Call ConnectAsync first.");

        var channel = await _consumeConnection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Set prefetch
        var prefetchCount = (ushort)(options.PrefetchCount ?? options.MaxConcurrency);
        await channel.BasicQosAsync(0, prefetchCount, false, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Ensure exchange exists
        await channel.ExchangeDeclareAsync(
            exchange: destination,
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Declare queue: {destination}.{groupId}
        var queueName = $"{destination}.{groupId}";
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Bind queue to exchange
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: destination,
            routingKey: string.Empty,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var subscription = new RabbitMqSubscription(channel, queueName, handler, _logger);
        await subscription.StartAsync(cancellationToken).ConfigureAwait(false);

        _subscriptions.Add(subscription);

        _logger.LogInformation("Subscribed to {Destination} with group {GroupId}", destination, groupId);
        return subscription;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var isHealthy = _publishConnection?.IsOpen == true && _consumeConnection?.IsOpen == true;
        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = isHealthy,
            TransportName = Name,
            Description = isHealthy ? "Both connections are open" : "One or both connections are closed"
        });
    }

    /// <inheritdoc />
    public IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener)
    {
        _listeners.Add(listener);
        return new ListenerDisposable(listener, _listeners);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await DisconnectAsync().ConfigureAwait(false);
        _channelPoolSemaphore.Dispose();
    }

    private ConnectionFactory CreateConnectionFactory() => new()
    {
        HostName = _config.HostName,
        Port = _config.Port,
        UserName = _config.UserName,
        Password = _config.Password,
        VirtualHost = _config.VirtualHost,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,
        NetworkRecoveryInterval = _config.NetworkRecoveryInterval,
        RequestedHeartbeat = _config.HeartbeatInterval
    };

    private async Task<IChannel> CreatePublishChannelAsync(CancellationToken cancellationToken)
    {
        return await _publishConnection!.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<IChannel> AcquirePublishChannelAsync(CancellationToken cancellationToken)
    {
        await _channelPoolSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (_publishChannels.TryTake(out var channel) && channel.IsOpen)
            return channel;

        // Channel was closed or pool empty; create a new one
        return await CreatePublishChannelAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ReleasePublishChannel(IChannel channel)
    {
        if (channel.IsOpen)
            _publishChannels.Add(channel);

        _channelPoolSemaphore.Release();
    }

    private void EmitEvent(
        LifecycleEventType type,
        string? destination = null,
        string? messageId = null,
        Exception? error = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var evt = new LifecycleEvent
        {
            EventType = type,
            TransportName = Name,
            Destination = destination,
            MessageId = messageId,
            Error = error,
            Metadata = metadata
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

    private sealed class ListenerDisposable(Action<LifecycleEvent> listener, ConcurrentBag<Action<LifecycleEvent>> listeners) : IDisposable
    {
        public void Dispose()
        {
            _ = listener;
            _ = listeners;
        }
    }
}
