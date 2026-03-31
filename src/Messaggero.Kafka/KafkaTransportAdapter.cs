using Confluent.Kafka;
using Messaggero.Abstractions;
using Messaggero.Errors;
using Messaggero.Model;

namespace Messaggero.Kafka;

/// <summary>
/// Kafka transport adapter using Confluent.Kafka.
/// </summary>
public sealed class KafkaTransportAdapter : ITransportAdapter
{
    private readonly KafkaOptions _options;
    private IProducer<string, byte[]>? _producer;
    private readonly List<(IConsumer<string, byte[]> Consumer, CancellationTokenSource Cts)> _consumers = [];
    private readonly Lock _lock = new();

    public KafkaTransportAdapter(string name, KafkaOptions options)
    {
        Name = name;
        _options = options;
    }

    public string Name { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        };

        foreach (var (key, value) in _options.ProducerConfig)
            producerConfig.Set(key, value);

        _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var (consumer, cts) in _consumers)
            {
                cts.Cancel();
                consumer.Close();
                cts.Dispose();
            }
            _consumers.Clear();
        }

        if (_producer is not null)
        {
            _producer.Flush(cancellationToken);
            _producer.Dispose();
            _producer = null;
        }

        await Task.CompletedTask;
    }

    public async Task<TransportOutcome> PublishAsync(Message message, Destination destination, CancellationToken cancellationToken)
    {
        if (_producer is null)
            return new TransportOutcome
            {
                TransportName = Name,
                Success = false,
                Error = new PublishFailure(Name, "Kafka producer not started.")
            };

        try
        {
            var kafkaMessage = new Message<string, byte[]>
            {
                Key = message.Id,
                Value = message.Payload.ToArray(),
                Headers = ConvertHeaders(message.Headers)
            };

            var result = await _producer.ProduceAsync(destination.Name, kafkaMessage, cancellationToken).ConfigureAwait(false);

            return new TransportOutcome
            {
                TransportName = Name,
                Success = true,
                BrokerMetadata = new Dictionary<string, string>
                {
                    ["partition"] = result.Partition.Value.ToString(),
                    ["offset"] = result.Offset.Value.ToString(),
                    ["topic"] = result.Topic
                }
            };
        }
        catch (ProduceException<string, byte[]> ex)
        {
            return new TransportOutcome
            {
                TransportName = Name,
                Success = false,
                Error = new PublishFailure(Name, $"Kafka publish failed: {ex.Error.Reason}", ex)
            };
        }
    }

    public Task SubscribeAsync(Destination destination, Func<Message, CancellationToken, Task> onMessage, CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            MaxPartitionFetchBytes = _options.PrefetchCount * 1024
        };

        foreach (var (key, value) in _options.ConsumerConfig)
            consumerConfig.Set(key, value);

        var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();
        consumer.Subscribe(destination.Name);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_lock)
        {
            _consumers.Add((consumer, cts));
        }

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(cts.Token);
                        if (result?.Message is null) continue;

                        var headers = new MessageHeaders();
                        if (result.Message.Headers is not null)
                        {
                            foreach (var header in result.Message.Headers)
                            {
                                headers.Set(header.Key, System.Text.Encoding.UTF8.GetString(header.GetValueBytes()));
                            }
                        }

                        var message = new Message
                        {
                            Id = result.Message.Key ?? Guid.NewGuid().ToString("N"),
                            Type = headers.TryGetValue("message-type", out var mt) ? mt : destination.Name,
                            Payload = result.Message.Value,
                            Headers = headers,
                            Timestamp = new DateTimeOffset(result.Message.Timestamp.UtcDateTime, TimeSpan.Zero),
                            SourceTransport = Name
                        };

                        // Store the topic partition offset for ack/nack
                        headers.Set("kafka-topic", result.Topic);
                        headers.Set("kafka-partition", result.Partition.Value.ToString());
                        headers.Set("kafka-offset", result.Offset.Value.ToString());

                        await onMessage(message, cts.Token).ConfigureAwait(false);
                    }
                    catch (ConsumeException)
                    {
                        // Logged by caller; continue consuming
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken)
    {
        // Commit offset for the consumed message
        if (message.Headers.TryGetValue("kafka-topic", out var topic)
            && message.Headers.TryGetValue("kafka-partition", out var partStr)
            && message.Headers.TryGetValue("kafka-offset", out var offStr)
            && int.TryParse(partStr, out var partition)
            && long.TryParse(offStr, out var offset))
        {
            lock (_lock)
            {
                foreach (var (consumer, _) in _consumers)
                {
                    try
                    {
                        consumer.StoreOffset(new TopicPartitionOffset(topic, new Partition(partition), new Offset(offset + 1)));
                        consumer.Commit();
                    }
                    catch
                    {
                        // Best effort
                    }
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task RejectAsync(Message message, CancellationToken cancellationToken)
    {
        // For Kafka, rejection means not committing the offset.
        // The message will be replayed on consumer restart.
        // If dead-letter is configured, the caller (RetryExecutor) handles DLT routing.
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static Headers ConvertHeaders(MessageHeaders headers)
    {
        var kafkaHeaders = new Headers();
        foreach (var (key, value) in headers)
        {
            kafkaHeaders.Add(key, System.Text.Encoding.UTF8.GetBytes(value));
        }
        return kafkaHeaders;
    }
}
