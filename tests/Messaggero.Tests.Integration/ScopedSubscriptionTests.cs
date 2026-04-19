using Assertivo;
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
/// T068: Handler scoped to Kafka only — messages on RabbitMQ do NOT invoke it.
/// </summary>
public class ScopedSubscriptionTests
{
    private sealed record OrderPlaced(string OrderId);

    private sealed class ScopedHandler : IMessageHandler<OrderPlaced>
    {
        public List<(OrderPlaced Event, string SourceTransport)> Received { get; } = [];
        private readonly SemaphoreSlim _signal = new(0);

        public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
        {
            Received.Add((message, context.SourceTransport));
            _signal.Release();
            return Task.CompletedTask;
        }

        public Task WaitForMessage(TimeSpan timeout) => _signal.WaitAsync(timeout);
    }

    [Fact]
    public async Task ScopedHandler_OnlyReceivesFromScopedTransport()
    {
        var kafkaAdapter = new InMemoryTransportAdapter("kafka");
        var rabbitAdapter = new InMemoryTransportAdapter("rabbitmq");
        var handler = new ScopedHandler();

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
                    HandlerType = typeof(ScopedHandler),
                    MessageClrType = typeof(OrderPlaced),
                    MaxConcurrency = 1,
                    TransportScope = "kafka"  // Scoped to kafka only
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

        // Publish to RabbitMQ — handler should NOT receive
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

        // Short wait to allow any (incorrect) dispatch
        await Task.Delay(200);
        handler.Received.Should().BeEmpty("handler is scoped to kafka only");

        // Publish to Kafka — handler SHOULD receive
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

        await handler.WaitForMessage(TimeSpan.FromSeconds(5));
        handler.Received.Should().ContainSingle()
            .Which.SourceTransport.Should().Be("kafka");

        await dispatcher.StopAsync(CancellationToken.None);
        await kafkaAdapter.DisposeAsync();
        await rabbitAdapter.DisposeAsync();
    }
}
