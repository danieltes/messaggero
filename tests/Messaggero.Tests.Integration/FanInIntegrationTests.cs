using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Messaggero.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaggero.Tests.Integration;

/// <summary>
/// T066: Fan-in — one handler receives from multiple transports.
/// </summary>
public class FanInIntegrationTests
{
    private sealed record OrderPlaced(string OrderId);

    private sealed class OrderHandler : IMessageHandler<OrderPlaced>
    {
        public List<(OrderPlaced Event, string SourceTransport)> Received { get; } = [];

        public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
        {
            lock (Received)
            {
                Received.Add((message, context.SourceTransport));
            }
            return Task.CompletedTask;
        }

        public async Task WaitForMessages(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (Received)
                {
                    if (Received.Count >= count) return;
                }
                await Task.Delay(50);
            }
        }
    }

    [Fact]
    public async Task Handler_ReceivesFromMultipleTransports_WithCorrectSourceMetadata()
    {
        var kafkaAdapter = new InMemoryTransportAdapter("kafka");
        var rabbitAdapter = new InMemoryTransportAdapter("rabbitmq");
        var handler = new OrderHandler();

        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var sp = services.BuildServiceProvider();

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["kafka", "rabbitmq"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["kafka"] = new TransportRegistration
                {
                    Name = "kafka",
                    AdapterFactory = _ => kafkaAdapter,
                    Options = new TransportOptions
                    {
                        RetryPolicy = new RetryPolicyOptions { MaxAttempts = 1, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) }
                    }
                },
                ["rabbitmq"] = new TransportRegistration
                {
                    Name = "rabbitmq",
                    AdapterFactory = _ => rabbitAdapter,
                    Options = new TransportOptions
                    {
                        RetryPolicy = new RetryPolicyOptions { MaxAttempts = 1, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) }
                    }
                }
            },
            Handlers =
            [
                new HandlerRegistration
                {
                    MessageType = "OrderPlaced",
                    HandlerType = typeof(OrderHandler),
                    MessageClrType = typeof(OrderPlaced),
                    MaxConcurrency = 1
                }
            ],
            DefaultSerializer = serializer
        };

        var adapters = new Dictionary<string, ITransportAdapter>
        {
            ["kafka"] = kafkaAdapter,
            ["rabbitmq"] = rabbitAdapter
        };

        await kafkaAdapter.StartAsync(CancellationToken.None);
        await rabbitAdapter.StartAsync(CancellationToken.None);

        var dispatcher = new HandlerDispatcher(config, adapters, sp, NullLogger.Instance);
        await dispatcher.StartAsync(CancellationToken.None);

        // Publish to Kafka
        var kafkaPayload = serializer.Serialize(new OrderPlaced("ORD-K1"));
        await kafkaAdapter.PublishAsync(new Message
        {
            Id = "kafka-msg",
            Type = "OrderPlaced",
            Payload = kafkaPayload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "kafka"
        }, new Destination { Name = "orderplaced" }, CancellationToken.None);

        // Publish to RabbitMQ
        var rabbitPayload = serializer.Serialize(new OrderPlaced("ORD-R1"));
        await rabbitAdapter.PublishAsync(new Message
        {
            Id = "rabbit-msg",
            Type = "OrderPlaced",
            Payload = rabbitPayload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "rabbitmq"
        }, new Destination { Name = "orderplaced" }, CancellationToken.None);

        await handler.WaitForMessages(2, TimeSpan.FromSeconds(5));

        handler.Received.Should().HaveCount(2);
        handler.Received.Select(r => r.SourceTransport).Should().BeEquivalentTo("kafka", "rabbitmq");

        await dispatcher.StopAsync(CancellationToken.None);
        await kafkaAdapter.DisposeAsync();
        await rabbitAdapter.DisposeAsync();
    }
}
