using FluentAssertions;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Contract;

/// <summary>
/// T058: StopAsync drains in-flight messages without loss.
/// </summary>
public class LifecycleContractTests
{
    [Fact]
    public async Task StartStop_Lifecycle_CompletesCleanly()
    {
        var adapter = new InMemoryTransportAdapter("lifecycle-test");

        await adapter.StartAsync(CancellationToken.None);

        // Publish some messages
        for (int i = 0; i < 5; i++)
        {
            var message = new Message
            {
                Id = $"lc-{i}",
                Type = "TestEvent",
                Payload = new byte[] { (byte)i },
                Headers = new MessageHeaders(),
                Timestamp = DateTimeOffset.UtcNow
            };
            await adapter.PublishAsync(message, new Destination { Name = "events" }, CancellationToken.None);
        }

        adapter.PublishedMessages.Should().HaveCount(5);

        await adapter.StopAsync(CancellationToken.None);

        // After stop, published messages are preserved (no data loss)
        adapter.PublishedMessages.Should().HaveCount(5);
    }

    [Fact]
    public async Task PublishAfterStop_ReturnsFailed()
    {
        var adapter = new InMemoryTransportAdapter("stopped-test");
        await adapter.StartAsync(CancellationToken.None);
        await adapter.StopAsync(CancellationToken.None);

        var message = new Message
        {
            Id = "post-stop",
            Type = "TestEvent",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var outcome = await adapter.PublishAsync(message, new Destination { Name = "events" }, CancellationToken.None);

        outcome.Success.Should().BeFalse();
        outcome.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_StopsAdapter()
    {
        var adapter = new InMemoryTransportAdapter("dispose-test");
        await adapter.StartAsync(CancellationToken.None);

        await adapter.DisposeAsync();

        var message = new Message
        {
            Id = "post-dispose",
            Type = "TestEvent",
            Payload = new byte[] { 1 },
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var outcome = await adapter.PublishAsync(message, new Destination { Name = "events" }, CancellationToken.None);
        outcome.Success.Should().BeFalse();
    }
}
