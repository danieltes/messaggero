using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Messaggero.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaggero.Tests.Integration;

/// <summary>
/// T059: OrderPlaced → Kafka, EmailRequested → RabbitMQ end-to-end using InMemory adapters.
/// </summary>
public class RoutingIntegrationTests
{
    private sealed record OrderPlaced(string OrderId);
    private sealed record EmailRequested(string To);

    [Fact]
    public async Task DifferentMessageTypes_RouteToCorrectTransports()
    {
        var kafkaAdapter = new InMemoryTransportAdapter("kafka");
        var rabbitAdapter = new InMemoryTransportAdapter("rabbitmq");

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["kafka"] },
                new RoutingRule { MessageType = "EmailRequested", Transports = ["rabbitmq"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["kafka"] = new TransportRegistration
                {
                    Name = "kafka",
                    AdapterFactory = _ => kafkaAdapter,
                    Options = new TransportOptions()
                },
                ["rabbitmq"] = new TransportRegistration
                {
                    Name = "rabbitmq",
                    AdapterFactory = _ => rabbitAdapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer
        };

        await kafkaAdapter.StartAsync(CancellationToken.None);
        await rabbitAdapter.StartAsync(CancellationToken.None);

        var adapters = new Dictionary<string, ITransportAdapter>
        {
            ["kafka"] = kafkaAdapter,
            ["rabbitmq"] = rabbitAdapter
        };

        var bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);

        var orderResult = await bus.PublishAsync(new OrderPlaced("ORD-1"));
        var emailResult = await bus.PublishAsync(new EmailRequested("user@test.com"));

        orderResult.IsSuccess.Should().BeTrue();
        orderResult.Outcomes.Should().ContainSingle().Which.TransportName.Should().Be("kafka");
        kafkaAdapter.PublishedMessages.Should().ContainSingle();

        emailResult.IsSuccess.Should().BeTrue();
        emailResult.Outcomes.Should().ContainSingle().Which.TransportName.Should().Be("rabbitmq");
        rabbitAdapter.PublishedMessages.Should().ContainSingle();

        // Cross-check: Kafka didn't get email, RabbitMQ didn't get order
        kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"));
        rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"));
    }
}
