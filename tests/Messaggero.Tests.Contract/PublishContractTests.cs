using Assertivo;
using Messaggero.Abstractions;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Contract;

public class PublishContractTests
{
    private sealed record OrderPlaced(string OrderId, decimal Amount);

    /// <summary>
    /// T049: publish succeeds → TransportOutcome.Success with metadata.
    /// Runs against InMemoryTransportAdapter.
    /// </summary>
    [Fact]
    public async Task PublishAsync_Success_ReturnsSuccessOutcomeWithMetadata()
    {
        var adapter = new InMemoryTransportAdapter("contract-test");
        await adapter.StartAsync(CancellationToken.None);

        var serializer = new Messaggero.Serialization.JsonMessageSerializer();
        var payload = serializer.Serialize(new OrderPlaced("ORD-1", 100m));

        var message = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "OrderPlaced",
            Payload = payload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var destination = new Destination { Name = "orders" };
        var outcome = await adapter.PublishAsync(message, destination, CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.TransportName.Should().Be("contract-test");
        Assert.NotNull(outcome.BrokerMetadata);
        outcome.Error.Should().BeNull();

        var published = Assert.Single(adapter.PublishedMessages);
        published.Id.Should().Be(message.Id);

        await adapter.DisposeAsync();
    }

    /// <summary>
    /// T050: publish to unavailable broker → TransportOutcome with PublishFailure.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AdapterNotStarted_ReturnsFailureOutcome()
    {
        var adapter = new InMemoryTransportAdapter("contract-test");
        // Deliberately NOT calling StartAsync

        var message = new Message
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "OrderPlaced",
            Payload = new byte[] { 1, 2, 3 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var destination = new Destination { Name = "orders" };
        var outcome = await adapter.PublishAsync(message, destination, CancellationToken.None);

        outcome.Success.Should().BeFalse();
        outcome.TransportName.Should().Be("contract-test");
        outcome.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishAsync_DeliveredToSubscribers()
    {
        var adapter = new InMemoryTransportAdapter("contract-test");
        await adapter.StartAsync(CancellationToken.None);

        Message? received = null;
        var destination = new Destination { Name = "orders" };
        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received = msg;
            return Task.CompletedTask;
        }, CancellationToken.None);

        var message = new Message
        {
            Id = "msg-1",
            Type = "OrderPlaced",
            Payload = new byte[] { 42 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);

        received.Should().NotBeNull();
        received!.Id.Should().Be("msg-1");
        received.SourceTransport.Should().Be("contract-test");

        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task AcknowledgeAsync_RemovesFromPending()
    {
        var adapter = new InMemoryTransportAdapter("contract-test");
        await adapter.StartAsync(CancellationToken.None);

        Message? received = null;
        var destination = new Destination { Name = "orders" };
        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received = msg;
            return Task.CompletedTask;
        }, CancellationToken.None);

        var message = new Message
        {
            Id = "msg-ack",
            Type = "OrderPlaced",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);
        Assert.Single(adapter.PendingMessages);

        await adapter.AcknowledgeAsync(received!, CancellationToken.None);
        Assert.Empty(adapter.PendingMessages);

        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task RejectAsync_MovesToDeadLetter()
    {
        var adapter = new InMemoryTransportAdapter("contract-test");
        await adapter.StartAsync(CancellationToken.None);

        Message? received = null;
        var destination = new Destination { Name = "orders" };
        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received = msg;
            return Task.CompletedTask;
        }, CancellationToken.None);

        var message = new Message
        {
            Id = "msg-reject",
            Type = "OrderPlaced",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);
        await adapter.RejectAsync(received!, CancellationToken.None);

        Assert.Empty(adapter.PendingMessages);
        var deadLetter = Assert.Single(adapter.DeadLetterMessages);
        deadLetter.Id.Should().Be("msg-reject");

        await adapter.DisposeAsync();
    }
}
