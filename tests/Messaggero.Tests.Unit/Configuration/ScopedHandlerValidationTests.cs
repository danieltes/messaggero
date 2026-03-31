using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Errors;
using Messaggero.Model;
using Messaggero.Testing;
using Xunit;

namespace Messaggero.Tests.Unit.Configuration;

/// <summary>
/// T069: Register handler scoped to unregistered transport → TransportNotFoundException.
/// </summary>
public class ScopedHandlerValidationTests
{
    private sealed record OrderPlaced(string OrderId);

    private sealed class OrderHandler : IMessageHandler<OrderPlaced>
    {
        public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    [Fact]
    public void Build_ScopedHandlerWithUnregisteredTransport_ThrowsTransportNotFoundException()
    {
        var builder = new MessagingBuilder();
        builder.AddTransport("kafka", _ => new InMemoryTransportAdapter("kafka"));
        builder.Route<OrderPlaced>(r => r.ToTransport("kafka"));
        builder.RegisterHandler<OrderHandler, OrderPlaced>(opts =>
        {
            opts.TransportScope = "nonexistent";
        });

        var act = () => builder.Build();

        act.Should().Throw<TransportNotFoundException>()
            .Which.TransportName.Should().Be("nonexistent");
    }

    [Fact]
    public void Build_ScopedHandlerWithRegisteredTransport_Succeeds()
    {
        var builder = new MessagingBuilder();
        builder.AddTransport("kafka", _ => new InMemoryTransportAdapter("kafka"));
        builder.Route<OrderPlaced>(r => r.ToTransport("kafka"));
        builder.RegisterHandler<OrderHandler, OrderPlaced>(opts =>
        {
            opts.TransportScope = "kafka";
        });

        var config = builder.Build();

        config.Handlers.Should().ContainSingle()
            .Which.TransportScope.Should().Be("kafka");
    }
}
