using Assertivo;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Contract;

public class SubscribeContractTests
{
    [Fact]
    public async Task SubscribeAsync_DeliversMessageWithCorrectPayloadAndSourceTransport()
    {
        var adapter = new InMemoryTransportAdapter("subscribe-test");
        await adapter.StartAsync(CancellationToken.None);

        Message? received = null;
        var destination = new Destination { Name = "events" };
        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received = msg;
            return Task.CompletedTask;
        }, CancellationToken.None);

        var message = new Message
        {
            Id = "sub-1",
            Type = "OrderPlaced",
            Payload = new byte[] { 10, 20, 30 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);

        received.Should().NotBeNull();
        received!.Id.Should().Be("sub-1");
        received.Type.Should().Be("OrderPlaced");
        received.Payload.ToArray().Should().BeEquivalentTo(new byte[] { 10, 20, 30 });
        received.SourceTransport.Should().Be("subscribe-test");

        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceive()
    {
        var adapter = new InMemoryTransportAdapter("multi-sub");
        await adapter.StartAsync(CancellationToken.None);

        var received1 = new List<Message>();
        var received2 = new List<Message>();
        var destination = new Destination { Name = "events" };

        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received1.Add(msg);
            return Task.CompletedTask;
        }, CancellationToken.None);

        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received2.Add(msg);
            return Task.CompletedTask;
        }, CancellationToken.None);

        var message = new Message
        {
            Id = "multi-1",
            Type = "OrderPlaced",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        await adapter.PublishAsync(message, destination, CancellationToken.None);

        received1.Should().ContainSingle();
        received2.Should().ContainSingle();

        await adapter.DisposeAsync();
    }
}
