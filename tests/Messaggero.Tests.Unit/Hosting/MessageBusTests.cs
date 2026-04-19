using Assertivo;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Errors;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Messaggero.Tests.Unit.Hosting;

public class MessageBusTests
{
    private sealed record OrderPlaced(string OrderId, decimal Amount);

    private readonly ITransportAdapter _adapter;
    private readonly MessageBus _bus;

    public MessageBusTests()
    {
        _adapter = Substitute.For<ITransportAdapter>();
        _adapter.Name.Returns("test-transport");
        _adapter.PublishAsync(Arg.Any<Message>(), Arg.Any<Destination>(), Arg.Any<CancellationToken>())
            .Returns(ci => new TransportOutcome
            {
                TransportName = "test-transport",
                Success = true,
                BrokerMetadata = new Dictionary<string, string> { ["offset"] = "42" }
            });

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule
                {
                    MessageType = "OrderPlaced",
                    Transports = ["test-transport"]
                }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["test-transport"] = new TransportRegistration
                {
                    Name = "test-transport",
                    AdapterFactory = _ => _adapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["test-transport"] = _adapter };
        _bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);
    }

    [Fact]
    public async Task PublishAsync_RoutesMessageToCorrectTransport()
    {
        var order = new OrderPlaced("ORD-1", 99.99m);

        var result = await _bus.PublishAsync(order);

        Assert.Single(result.Outcomes);
        result.Outcomes[0].TransportName.Should().Be("test-transport");
        result.Outcomes[0].Success.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_SerializesMessageAndCreatesEnvelope()
    {
        var order = new OrderPlaced("ORD-2", 50.00m);

        await _bus.PublishAsync(order);

        await _adapter.Received(1).PublishAsync(
            Arg.Is<Message>(m =>
                m.Type == "OrderPlaced" &&
                m.Payload.Length > 0 &&
                m.Headers.ContentType == "application/json"),
            Arg.Any<Destination>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_NoRoutingRule_ThrowsNoRouteFoundException()
    {
        var act = () => _bus.PublishAsync("unrouted string message");

        await Assert.ThrowsAsync<NoRouteFoundException>(act);
    }

    [Fact]
    public async Task PublishAsync_AdapterThrows_ReturnsFailureOutcome()
    {
        _adapter.PublishAsync(Arg.Any<Message>(), Arg.Any<Destination>(), Arg.Any<CancellationToken>())
            .Returns<TransportOutcome>(_ => throw new InvalidOperationException("Broker down"));

        var order = new OrderPlaced("ORD-3", 10.00m);
        var result = await _bus.PublishAsync(order);

        result.IsSuccess.Should().BeFalse();
        result.Outcomes[0].Success.Should().BeFalse();
        result.Outcomes[0].Error.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishAsync_SetsMessageIdOnResult()
    {
        var order = new OrderPlaced("ORD-4", 1.00m);

        var result = await _bus.PublishAsync(order);

        result.MessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PublishAsync_WithCustomHeaders_PassesHeadersToAdapter()
    {
        var headers = new MessageHeaders();
        headers.Set("correlation-id", "abc-123");

        var order = new OrderPlaced("ORD-5", 5.00m);
        await _bus.PublishAsync(order, headers);

        await _adapter.Received(1).PublishAsync(
            Arg.Is<Message>(m => m.Headers.ContainsKey("correlation-id") && m.Headers["correlation-id"] == "abc-123"),
            Arg.Any<Destination>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_TransportNotInRunningAdapters_ReturnsFailure()
    {
        // Config references "missing-transport" but no adapter registered
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["missing-transport"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["missing-transport"] = new TransportRegistration
                {
                    Name = "missing-transport",
                    AdapterFactory = _ => Substitute.For<ITransportAdapter>(),
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = new JsonMessageSerializer()
        };

        var bus = new MessageBus(config, new Dictionary<string, ITransportAdapter>(), NullLogger<MessageBus>.Instance);
        var result = await bus.PublishAsync(new OrderPlaced("ORD-6", 1.00m));

        result.IsSuccess.Should().BeFalse();
        result.Outcomes[0].Error.Should().BeOfType<PublishFailure>();
    }
}
