using System.Collections.Concurrent;
using Messaggero.Abstractions;
using Messaggero.Concurrency;
using Messaggero.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero;

/// <summary>
/// Core message bus implementation that orchestrates serialization, transport delegation,
/// subscription management, error strategies, and per-key ordered processing.
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly ITransportRouter _router;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<MessageBus> _logger;
    private readonly ConcurrentBag<SubscriptionHandle> _subscriptions = [];
    private readonly ConcurrentBag<Action<LifecycleEvent>> _listeners = [];
    private readonly List<IDisposable> _lifecycleRegistrations = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBus"/> class with a single transport (backward compatible).
    /// </summary>
    /// <param name="transport">The transport implementation to use.</param>
    /// <param name="serializer">The message serializer.</param>
    /// <param name="logger">Optional logger instance.</param>
    public MessageBus(IMessageBusTransport transport, IMessageSerializer serializer, ILogger<MessageBus>? logger = null)
        : this(CreateSingleTransportRouter(transport), serializer, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBus"/> class with a transport router.
    /// </summary>
    /// <param name="router">The transport router for resolving transports.</param>
    /// <param name="serializer">The message serializer.</param>
    /// <param name="logger">Optional logger instance.</param>
    internal MessageBus(ITransportRouter router, IMessageSerializer serializer, ILogger<MessageBus>? logger = null)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? NullLogger<MessageBus>.Instance;
    }

    private static TransportRouter CreateSingleTransportRouter(IMessageBusTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var transports = new Dictionary<string, IMessageBusTransport> { [transport.Name] = transport };
        return new TransportRouter(transports, [], [], transport.Name);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(
        string destination,
        T payload,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var body = _serializer.Serialize(payload);
        var metadata = new MessageMetadata
        {
            MessageId = Guid.CreateVersion7().ToString("N"),
            RoutingKey = options?.RoutingKey,
            Headers = options?.Headers ?? new Dictionary<string, string>(),
            Timestamp = DateTimeOffset.UtcNow,
            ContentType = _serializer.ContentType,
            CorrelationId = options?.CorrelationId
        };

        var transport = _router.ResolveTransport(destination, typeof(T));
        await transport.PublishAsync(destination, body, metadata, cancellationToken).ConfigureAwait(false);

        EmitLifecycleEvent(LifecycleEventType.MessagePublished,
            transportName: transport.Name, destination: destination, messageId: metadata.MessageId);
    }

    /// <inheritdoc />
    public async Task<ISubscriptionHandle> SubscribeAsync<T>(
        string destination,
        string groupId,
        IMessageHandler<T> handler,
        SubscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var effectiveOptions = options ?? new SubscriptionOptions();
        KeyPartitionedProcessor<object>? processor = null;

        if (effectiveOptions.MaxConcurrency > 1)
        {
            processor = new KeyPartitionedProcessor<object>(effectiveOptions.MaxConcurrency);
        }

        var transport = _router.ResolveTransport(destination);

        var transportSubscription = await transport.SubscribeAsync(
            destination,
            groupId,
            async (body, metadata, ct) =>
            {
                EmitLifecycleEvent(LifecycleEventType.MessageReceived,
                    transportName: transport.Name, destination: destination, messageId: metadata.MessageId);

                async Task ProcessMessageAsync(CancellationToken processingCt)
                {
                    var payload = _serializer.Deserialize<T>(body.Span);
                    var envelope = new MessageEnvelope<T>
                    {
                        MessageId = metadata.MessageId,
                        Payload = payload,
                        Destination = destination,
                        Headers = metadata.Headers,
                        RoutingKey = metadata.RoutingKey,
                        Timestamp = metadata.Timestamp,
                        ContentType = metadata.ContentType,
                        CorrelationId = metadata.CorrelationId
                    };

                    await ExecuteWithErrorStrategyAsync(
                        handler, envelope, effectiveOptions.ErrorStrategy, transport.Name, processingCt).ConfigureAwait(false);
                }

                if (processor is not null)
                {
                    await processor.EnqueueAsync(
                        metadata.RoutingKey,
                        ProcessMessageAsync).ConfigureAwait(false);
                }
                else
                {
                    await ProcessMessageAsync(ct).ConfigureAwait(false);
                }
            },
            effectiveOptions,
            cancellationToken).ConfigureAwait(false);

        var handle = new SubscriptionHandle(destination, groupId, transportSubscription, processor);
        _subscriptions.Add(handle);
        return handle;
    }

    private async Task ExecuteWithErrorStrategyAsync<T>(
        IMessageHandler<T> handler,
        MessageEnvelope<T> envelope,
        ErrorStrategy errorStrategy,
        string transportName,
        CancellationToken cancellationToken)
    {
        switch (errorStrategy.Type)
        {
            case ErrorStrategyType.Retry:
                await ExecuteWithRetryAsync(handler, envelope, errorStrategy, transportName, cancellationToken).ConfigureAwait(false);
                break;

            case ErrorStrategyType.DeadLetter:
                try
                {
                    await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    EmitLifecycleEvent(LifecycleEventType.MessageError,
                        transportName: transportName, destination: envelope.Destination, messageId: envelope.MessageId, error: ex);
                    _logger.LogWarning(ex, "Handler failed for message {MessageId}, forwarding to dead-letter destination {Destination}",
                        envelope.MessageId, errorStrategy.DeadLetterDestination);

                    if (errorStrategy.DeadLetterDestination is not null)
                    {
                        var body = _serializer.Serialize(envelope.Payload);
                        var metadata = new MessageMetadata
                        {
                            MessageId = envelope.MessageId,
                            RoutingKey = envelope.RoutingKey,
                            Headers = new Dictionary<string, string>(envelope.Headers)
                            {
                                ["x-dead-letter-reason"] = ex.Message,
                                ["x-original-destination"] = envelope.Destination
                            },
                            Timestamp = envelope.Timestamp,
                            ContentType = envelope.ContentType,
                            CorrelationId = envelope.CorrelationId
                        };
                        await _router.ResolveTransport(errorStrategy.DeadLetterDestination)
                            .PublishAsync(errorStrategy.DeadLetterDestination, body, metadata, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                break;

            case ErrorStrategyType.Reject:
                try
                {
                    await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    EmitLifecycleEvent(LifecycleEventType.MessageError,
                        transportName: transportName, destination: envelope.Destination, messageId: envelope.MessageId, error: ex);
                    _logger.LogWarning(ex, "Handler failed for message {MessageId}, rejecting (no requeue)",
                        envelope.MessageId);
                    // Message is discarded — no requeue, no dead-letter
                }
                break;
        }
    }

    private async Task ExecuteWithRetryAsync<T>(
        IMessageHandler<T> handler,
        MessageEnvelope<T> envelope,
        ErrorStrategy errorStrategy,
        string transportName,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = errorStrategy.RetryDelay;

        while (true)
        {
            try
            {
                await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
                return; // Success — exit
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                attempt++;
                if (attempt > errorStrategy.MaxRetries)
                {
                    EmitLifecycleEvent(LifecycleEventType.MessageError,
                        transportName: transportName, destination: envelope.Destination, messageId: envelope.MessageId, error: ex);
                    _logger.LogError(ex,
                        "Handler failed for message {MessageId} after {Attempts} retries, giving up",
                        envelope.MessageId, attempt);
                    throw;
                }

                _logger.LogWarning(ex,
                    "Handler failed for message {MessageId}, retry {Attempt}/{MaxRetries} after {Delay}ms",
                    envelope.MessageId, attempt, errorStrategy.MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * errorStrategy.RetryBackoffMultiplier,
                             TimeSpan.FromMinutes(5).TotalMilliseconds));
            }
        }
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entries = new List<TransportHealthEntry>();
        foreach (var (name, transport) in _router.Transports)
        {
            var result = await transport.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            entries.Add(new TransportHealthEntry
            {
                TransportName = name,
                IsHealthy = result.IsHealthy,
                Description = result.Description
            });
        }

        var allHealthy = entries.TrueForAll(e => e.IsHealthy);
        var anyHealthy = entries.Exists(e => e.IsHealthy);

        return new HealthCheckResult
        {
            IsHealthy = allHealthy,
            TransportName = entries.Count == 1 ? entries[0].TransportName : "Aggregate",
            Description = allHealthy ? "All transports healthy" :
                          anyHealthy ? "Some transports degraded" :
                          "All transports unhealthy",
            TransportEntries = entries
        };
    }

    /// <inheritdoc />
    public IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);

        // Forward lifecycle events from all transports
        foreach (var (_, transport) in _router.Transports)
        {
            _lifecycleRegistrations.Add(transport.OnLifecycleEvent(listener));
        }

        return new CompositeDisposable(_lifecycleRegistrations.ToList());
    }

    private void EmitLifecycleEvent(
        LifecycleEventType type,
        string transportName,
        string? destination = null,
        string? messageId = null,
        Exception? error = null)
    {
        var evt = new LifecycleEvent
        {
            EventType = type,
            TransportName = transportName,
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Dispose lifecycle registrations
        foreach (var reg in _lifecycleRegistrations)
        {
            reg.Dispose();
        }

        // Graceful shutdown: drain KeyPartitionedProcessors, then dispose subscriptions
        foreach (var sub in _subscriptions)
        {
            await sub.DrainAndDisposeAsync().ConfigureAwait(false);
        }

        // Dispose all transports
        foreach (var (_, transport) in _router.Transports)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class CompositeDisposable(List<IDisposable> disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var d in disposables)
            {
                d.Dispose();
            }
        }
    }

    private sealed class SubscriptionHandle : ISubscriptionHandle
    {
        private readonly ITransportSubscription _inner;
        private readonly KeyPartitionedProcessor<object>? _processor;

        public SubscriptionHandle(
            string destination,
            string groupId,
            ITransportSubscription inner,
            KeyPartitionedProcessor<object>? processor = null)
        {
            Destination = destination;
            GroupId = groupId;
            _inner = inner;
            _processor = processor;
        }

        public string Destination { get; }
        public string GroupId { get; }
        public bool IsActive => _inner.IsActive;

        /// <summary>
        /// Drains in-flight work from the processor before disposing the subscription.
        /// </summary>
        public async ValueTask DrainAndDisposeAsync()
        {
            if (_processor is not null)
            {
                await _processor.StopAsync().ConfigureAwait(false);
                await _processor.DisposeAsync().ConfigureAwait(false);
            }

            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_processor is not null)
            {
                await _processor.DisposeAsync().ConfigureAwait(false);
            }

            await _inner.DisposeAsync().ConfigureAwait(false);
        }
    }
}
