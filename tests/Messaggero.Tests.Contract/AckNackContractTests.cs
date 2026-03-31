using FluentAssertions;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Contract;

public class AckNackContractTests
{
    [Fact]
    public async Task AcknowledgeAsync_RemovesFromPending_PreventsRedelivery()
    {
        var adapter = new InMemoryTransportAdapter("ack-test");
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
            Id = "ack-1",
            Type = "OrderPlaced",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);
        adapter.PendingMessages.Should().ContainSingle();

        await adapter.AcknowledgeAsync(received!, CancellationToken.None);

        adapter.PendingMessages.Should().BeEmpty();
        adapter.DeadLetterMessages.Should().BeEmpty();

        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task RejectAsync_MovesToDeadLetter()
    {
        var adapter = new InMemoryTransportAdapter("nack-test");
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
            Id = "nack-1",
            Type = "OrderPlaced",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);
        await adapter.RejectAsync(received!, CancellationToken.None);

        adapter.PendingMessages.Should().BeEmpty();
        adapter.DeadLetterMessages.Should().ContainSingle()
            .Which.Id.Should().Be("nack-1");

        await adapter.DisposeAsync();
    }
}
