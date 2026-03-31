using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Model;
using Xunit;

namespace Messaggero.Tests.Unit.Examples;

/// <summary>
/// T056: Demonstrates that handlers can be instantiated and tested in isolation
/// without the library host (SC-011).
/// </summary>
public class HandlerIsolationExampleTests
{
    private sealed record OrderPlaced(string OrderId, decimal Amount);

    private sealed class OrderHandler : IMessageHandler<OrderPlaced>
    {
        public List<OrderPlaced> ProcessedOrders { get; } = [];

        public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
        {
            ProcessedOrders.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handler_CanBeTestedWithoutLibraryHost()
    {
        // A handler is a plain class — no base class, no framework dependency
        var handler = new OrderHandler();

        var context = new MessageContext
        {
            MessageId = "test-1",
            MessageType = nameof(OrderPlaced),
            SourceTransport = "test",
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            DeliveryAttempt = 1
        };

        var order = new OrderPlaced("ORD-42", 99.99m);
        await handler.HandleAsync(order, context, CancellationToken.None);

        handler.ProcessedOrders.Should().ContainSingle()
            .Which.OrderId.Should().Be("ORD-42");
    }

    [Fact]
    public async Task Handler_ReceivesCorrectDeliveryAttempt()
    {
        var handler = new OrderHandler();

        var context = new MessageContext
        {
            MessageId = "test-2",
            MessageType = nameof(OrderPlaced),
            SourceTransport = "kafka",
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            DeliveryAttempt = 3  // Simulating a retry
        };

        await handler.HandleAsync(new OrderPlaced("ORD-1", 1m), context, CancellationToken.None);

        // The handler receives the context as-is — no framework magic
        handler.ProcessedOrders.Should().ContainSingle();
    }
}
