using FluentAssertions;
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
/// T057: Adapter failure isolation — one adapter fails, others continue.
/// </summary>
public class AdapterIsolationTests
{
    private sealed record OrderPlaced(string OrderId, decimal Amount);

    [Fact]
    public async Task OneAdapterDown_OthersContinuePublishing()
    {
        var healthyAdapter = new InMemoryTransportAdapter("healthy");
        var failingAdapter = new FailingTransportAdapter("failing");

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["healthy", "failing"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["healthy"] = new TransportRegistration
                {
                    Name = "healthy",
                    AdapterFactory = _ => healthyAdapter,
                    Options = new TransportOptions()
                },
                ["failing"] = new TransportRegistration
                {
                    Name = "failing",
                    AdapterFactory = _ => failingAdapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer
        };

        var adapters = new Dictionary<string, ITransportAdapter>
        {
            ["healthy"] = healthyAdapter,
            ["failing"] = failingAdapter
        };

        await healthyAdapter.StartAsync(CancellationToken.None);

        var bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);
        var result = await bus.PublishAsync(new OrderPlaced("ORD-1", 100m));

        // Fan-out: one succeeds, one fails
        result.Outcomes.Should().HaveCount(2);
        result.IsSuccess.Should().BeFalse(); // overall is false since one failed

        var healthyOutcome = result.Outcomes.Single(o => o.TransportName == "healthy");
        healthyOutcome.Success.Should().BeTrue();

        var failingOutcome = result.Outcomes.Single(o => o.TransportName == "failing");
        failingOutcome.Success.Should().BeFalse();
        failingOutcome.Error.Should().NotBeNull();

        // Healthy adapter received the message
        healthyAdapter.PublishedMessages.Should().ContainSingle();

        await healthyAdapter.DisposeAsync();
    }

    private sealed class FailingTransportAdapter : ITransportAdapter
    {
        public FailingTransportAdapter(string name) => Name = name;
        public string Name { get; }
        public Task StartAsync(CancellationToken ct) => throw new InvalidOperationException("Connection refused");
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TransportOutcome> PublishAsync(Message msg, Destination dest, CancellationToken ct)
            => throw new InvalidOperationException("Broker unavailable");
        public Task SubscribeAsync(Destination dest, Func<Message, CancellationToken, Task> onMsg, CancellationToken ct) => Task.CompletedTask;
        public Task AcknowledgeAsync(Message msg, CancellationToken ct) => Task.CompletedTask;
        public Task RejectAsync(Message msg, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
