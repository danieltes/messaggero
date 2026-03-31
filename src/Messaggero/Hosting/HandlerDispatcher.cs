using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Model;
using Messaggero.Observability;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Messaggero.Hosting;

/// <summary>
/// Dispatches incoming messages to registered handlers.
/// Uses Channel&lt;T&gt; for backpressure and SemaphoreSlim for concurrency limiting.
/// </summary>
public sealed class HandlerDispatcher
{
    private readonly MessagingConfiguration _config;
    private readonly IReadOnlyDictionary<string, ITransportAdapter> _adapters;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly List<CancellationTokenSource> _subscriptionCts = [];

    public HandlerDispatcher(
        MessagingConfiguration config,
        IReadOnlyDictionary<string, ITransportAdapter> adapters,
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        _config = config;
        _adapters = adapters;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var handler in _config.Handlers)
        {
            var transportsToSubscribe = GetTransportsForHandler(handler);

            foreach (var (transportName, adapter) in transportsToSubscribe)
            {
                var registration = _config.Transports[transportName];
                var rule = _config.RoutingTable.Rules.GetValueOrDefault(handler.MessageType);
                var destination = rule?.Destination ?? new Destination { Name = handler.MessageType.ToLowerInvariant() };

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _subscriptionCts.Add(cts);

                var prefetchLimit = registration.Options.PrefetchCount;
                var channel = Channel.CreateBounded<Message>(new BoundedChannelOptions(prefetchLimit)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true
                });

                // Start the consumer dispatch loop
                _ = Task.Run(async () =>
                {
                    var semaphore = new SemaphoreSlim(handler.MaxConcurrency, handler.MaxConcurrency);
                    var retryExecutor = new RetryExecutor(registration.Options.RetryPolicy, _logger);

                    await foreach (var message in channel.Reader.ReadAllAsync(cts.Token))
                    {
                        await semaphore.WaitAsync(cts.Token).ConfigureAwait(false);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await DispatchToHandler(handler, message, adapter, retryExecutor, registration, cts.Token)
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, cts.Token);
                    }
                }, cts.Token);

                // Subscribe to the adapter
                await adapter.SubscribeAsync(
                    destination,
                    async (msg, ct) => await channel.Writer.WriteAsync(msg, ct).ConfigureAwait(false),
                    cts.Token).ConfigureAwait(false);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var cts in _subscriptionCts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            cts.Dispose();
        }
        _subscriptionCts.Clear();
    }

    private IEnumerable<(string Name, ITransportAdapter Adapter)> GetTransportsForHandler(HandlerRegistration handler)
    {
        if (handler.TransportScope is not null)
        {
            if (_adapters.TryGetValue(handler.TransportScope, out var adapter))
                yield return (handler.TransportScope, adapter);
            yield break;
        }

        // Fan-in: subscribe to all active transports
        foreach (var (name, adapter) in _adapters)
        {
            yield return (name, adapter);
        }
    }

    private async Task DispatchToHandler(
        HandlerRegistration handler,
        Message message,
        ITransportAdapter adapter,
        RetryExecutor retryExecutor,
        TransportRegistration registration,
        CancellationToken cancellationToken)
    {
        var serializer = registration.Serializer ?? _config.DefaultSerializer;

        var context = new MessageContext
        {
            MessageId = message.Id,
            MessageType = message.Type,
            SourceTransport = message.SourceTransport ?? adapter.Name,
            Headers = message.Headers,
            Timestamp = message.Timestamp,
            DeliveryAttempt = 1
        };

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var activity = _config.ObservabilityEnabled
                ? MessagingActivitySource.StartConsume(message.Type, adapter.Name, message.Id)
                : null;

            await retryExecutor.ExecuteAsync(async (attempt, ct) =>
            {
                context = new MessageContext
                {
                    MessageId = context.MessageId,
                    MessageType = context.MessageType,
                    SourceTransport = context.SourceTransport,
                    Headers = context.Headers,
                    Timestamp = context.Timestamp,
                    DeliveryAttempt = attempt
                };
                var handlerInstance = _serviceProvider.GetService(handler.HandlerType)!;

                // Invoke HandleAsync via reflection since we don't know TMessage at compile time
                var handleMethod = handler.HandlerType
                    .GetMethod("HandleAsync")!;

                // Deserialize the payload using a helper to avoid boxing ReadOnlySpan
                var deserialized = DeserializePayload(serializer, message.Payload.ToArray(), handler.MessageClrType);

                var task = (Task)handleMethod.Invoke(handlerInstance, [deserialized, context, ct])!;
                await task.ConfigureAwait(false);

            }, cancellationToken).ConfigureAwait(false);

            sw.Stop();
            if (_config.ObservabilityEnabled)
            {
                MessagingMetrics.MessagesConsumed.Add(1,
                    new KeyValuePair<string, object?>("transport", adapter.Name),
                    new KeyValuePair<string, object?>("message_type", message.Type));
                MessagingMetrics.ConsumeDuration.Record(sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("transport", adapter.Name),
                    new KeyValuePair<string, object?>("message_type", message.Type));
            }

            _logger.LogInformation("Handled {MessageType} ({MessageId}) from {TransportName} in {ElapsedMs}ms",
                message.Type, message.Id, adapter.Name, sw.Elapsed.TotalMilliseconds);

            await adapter.AcknowledgeAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler {HandlerType} failed for message {MessageId}", handler.HandlerType.Name, message.Id);

            await adapter.RejectAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private static object DeserializePayload(IMessageSerializer serializer, byte[] data, Type messageType)
    {
        var method = typeof(HandlerDispatcher)
            .GetMethod(nameof(DeserializeGeneric), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(messageType);
        return method.Invoke(null, [serializer, data])!;
    }

    private static TMessage DeserializeGeneric<TMessage>(IMessageSerializer serializer, byte[] data) where TMessage : class
    {
        return serializer.Deserialize<TMessage>(data.AsSpan());
    }
}
