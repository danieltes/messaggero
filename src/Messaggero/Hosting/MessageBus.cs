using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Errors;
using Messaggero.Model;
using Messaggero.Observability;
using Microsoft.Extensions.Logging;

namespace Messaggero.Hosting;

/// <summary>
/// Default implementation of <see cref="IMessageBus"/>.
/// Resolves routing rules, serializes messages, and publishes to resolved transports.
/// </summary>
public sealed class MessageBus : IMessageBus
{
    private readonly MessagingConfiguration _config;
    private readonly IReadOnlyDictionary<string, ITransportAdapter> _adapters;
    private readonly ILogger<MessageBus> _logger;

    public MessageBus(
        MessagingConfiguration config,
        IReadOnlyDictionary<string, ITransportAdapter> adapters,
        ILogger<MessageBus> logger)
    {
        _config = config;
        _adapters = adapters;
        _logger = logger;
    }

    public Task<PublishResult> PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        return PublishAsync(message, new MessageHeaders(), cancellationToken);
    }

    public async Task<PublishResult> PublishAsync<TMessage>(TMessage message, MessageHeaders headers, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        var messageType = typeof(TMessage).Name;
        var rule = _config.RoutingTable.Resolve(messageType);
        var destination = rule.Destination ?? new Destination { Name = messageType.ToLowerInvariant() };

        var messageId = Guid.NewGuid().ToString("N");
        var outcomes = new List<TransportOutcome>();

        _logger.LogDebug("Publishing {MessageType} ({MessageId}) to {TransportCount} transport(s)",
            messageType, messageId, rule.Transports.Count);

        foreach (var transportName in rule.Transports)
        {
            using var activity = _config.ObservabilityEnabled
                ? MessagingActivitySource.StartPublish(messageType, transportName, destination.Name)
                : null;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (!_adapters.TryGetValue(transportName, out var adapter))
            {
                outcomes.Add(new TransportOutcome
                {
                    TransportName = transportName,
                    Success = false,
                    Error = new PublishFailure(transportName, $"Transport '{transportName}' not found in running adapters.")
                });
                continue;
            }

            var registration = _config.Transports[transportName];
            var serializer = registration.Serializer ?? _config.DefaultSerializer;

            var payload = serializer.Serialize(message);
            headers.ContentType = serializer.ContentType;

            var envelope = new Message
            {
                Id = messageId,
                Type = messageType,
                Payload = payload,
                Headers = headers,
                Timestamp = DateTimeOffset.UtcNow
            };

            try
            {
                var outcome = await adapter.PublishAsync(envelope, destination, cancellationToken).ConfigureAwait(false);
                outcomes.Add(outcome);

                sw.Stop();
                if (_config.ObservabilityEnabled)
                    MessagingMetrics.PublishDuration.Record(sw.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("transport", transportName),
                        new KeyValuePair<string, object?>("message_type", messageType));

                if (outcome.Success)
                {
                    if (_config.ObservabilityEnabled)
                        MessagingMetrics.MessagesPublished.Add(1,
                            new KeyValuePair<string, object?>("transport", transportName),
                            new KeyValuePair<string, object?>("message_type", messageType));

                    _logger.LogInformation("Published {MessageType} ({MessageId}) to {TransportName} in {ElapsedMs}ms",
                        messageType, messageId, transportName, sw.Elapsed.TotalMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Publish {MessageType} ({MessageId}) to {TransportName} returned failure: {Error}",
                        messageType, messageId, transportName, outcome.Error?.BrokerError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Publish to transport {TransportName} failed for message {MessageId}", transportName, messageId);
                outcomes.Add(new TransportOutcome
                {
                    TransportName = transportName,
                    Success = false,
                    Error = new PublishFailure(transportName, $"Publish failed: {ex.Message}", ex)
                });
            }
        }

        return new PublishResult
        {
            MessageId = messageId,
            Outcomes = outcomes.AsReadOnly()
        };
    }
}
