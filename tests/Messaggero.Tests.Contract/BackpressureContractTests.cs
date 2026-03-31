using FluentAssertions;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Contract;

public class BackpressureContractTests
{
    [Fact]
    public async Task InMemoryAdapter_SubscriberReceivesAllMessages()
    {
        var adapter = new InMemoryTransportAdapter("bp-test");
        await adapter.StartAsync(CancellationToken.None);

        var received = new List<Message>();
        var destination = new Destination { Name = "orders" };
        await adapter.SubscribeAsync(destination, (msg, ct) =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        }, CancellationToken.None);

        // Publish multiple messages
        for (int i = 0; i < 10; i++)
        {
            var message = new Message
            {
                Id = $"bp-{i}",
                Type = "OrderPlaced",
                Payload = new byte[] { (byte)i },
                Headers = new MessageHeaders(),
                Timestamp = DateTimeOffset.UtcNow
            };
            await adapter.PublishAsync(message, destination, CancellationToken.None);
        }

        received.Should().HaveCount(10);
        adapter.PublishedMessages.Should().HaveCount(10);

        await adapter.DisposeAsync();
    }
}
