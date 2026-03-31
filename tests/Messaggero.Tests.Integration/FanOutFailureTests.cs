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
/// T060: Fan-out partial failure — one adapter down, other succeeds.
/// </summary>
public class FanOutFailureTests
{
    private sealed record ImportantEvent(string Data);

    [Fact]
    public async Task FanOut_OneAdapterFails_OtherSucceeds_BothReported()
    {
        var healthyAdapter = new InMemoryTransportAdapter("healthy");
        var failingAdapter = new AlwaysFailAdapter("unhealthy");

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "ImportantEvent", Transports = ["healthy", "unhealthy"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["healthy"] = new TransportRegistration
                {
                    Name = "healthy",
                    AdapterFactory = _ => healthyAdapter,
                    Options = new TransportOptions()
                },
                ["unhealthy"] = new TransportRegistration
                {
                    Name = "unhealthy",
                    AdapterFactory = _ => failingAdapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer
        };

        await healthyAdapter.StartAsync(CancellationToken.None);

        var adapters = new Dictionary<string, ITransportAdapter>
        {
            ["healthy"] = healthyAdapter,
            ["unhealthy"] = failingAdapter
        };

        var bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);
        var result = await bus.PublishAsync(new ImportantEvent("critical-data"));

        result.Outcomes.Should().HaveCount(2);
        result.IsSuccess.Should().BeFalse();

        var success = result.Outcomes.Single(o => o.TransportName == "healthy");
        success.Success.Should().BeTrue();

        var failure = result.Outcomes.Single(o => o.TransportName == "unhealthy");
        failure.Success.Should().BeFalse();
        failure.Error.Should().NotBeNull();
    }

    private sealed class AlwaysFailAdapter : ITransportAdapter
    {
        public AlwaysFailAdapter(string name) => Name = name;
        public string Name { get; }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<TransportOutcome> PublishAsync(Message msg, Destination dest, CancellationToken ct)
            => throw new InvalidOperationException("Simulated broker failure");
        public Task SubscribeAsync(Destination dest, Func<Message, CancellationToken, Task> onMsg, CancellationToken ct) => Task.CompletedTask;
        public Task AcknowledgeAsync(Message msg, CancellationToken ct) => Task.CompletedTask;
        public Task RejectAsync(Message msg, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
