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
/// T067: Fan-in with degraded transport — handler continues receiving from available transport.
/// </summary>
public class FanInDegradedTests
{
    private sealed record OrderPlaced(string OrderId);

    private sealed class OrderHandler : IMessageHandler<OrderPlaced>
    {
        public List<(OrderPlaced Event, string SourceTransport)> Received { get; } = [];
        private readonly SemaphoreSlim _signal = new(0);

        public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken cancellationToken)
        {
            Received.Add((message, context.SourceTransport));
            _signal.Release();
            return Task.CompletedTask;
        }

        public Task WaitForMessage(TimeSpan timeout) => _signal.WaitAsync(timeout);
    }

    [Fact]
    public async Task DegradedTransport_HandlerStillReceivesFromHealthyTransport()
    {
        var healthyAdapter = new InMemoryTransportAdapter("healthy");
        var handler = new OrderHandler();

        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var sp = services.BuildServiceProvider();

        var serializer = new JsonMessageSerializer();

        // Only the healthy adapter is started; "degraded" adapter exists in config
        // but HandlerDispatcher only subscribes to available adapters
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["healthy"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["healthy"] = new TransportRegistration
                {
                    Name = "healthy",
                    AdapterFactory = _ => healthyAdapter,
                    Options = new TransportOptions
                    {
                        RetryPolicy = new RetryPolicyOptions { MaxAttempts = 1, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) }
                    }
                }
            },
            Handlers =
            [
                new HandlerRegistration
                {
                    MessageType = "OrderPlaced",
                    HandlerType = typeof(OrderHandler),
                    MessageClrType = typeof(OrderPlaced),
                    MaxConcurrency = 1
                }
            ],
            DefaultSerializer = serializer
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["healthy"] = healthyAdapter };
        await healthyAdapter.StartAsync(CancellationToken.None);

        var dispatcher = new HandlerDispatcher(config, adapters, sp, NullLogger.Instance);
        await dispatcher.StartAsync(CancellationToken.None);

        var payload = serializer.Serialize(new OrderPlaced("ORD-H1"));
        await healthyAdapter.PublishAsync(new Message
        {
            Id = "h-msg",
            Type = "OrderPlaced",
            Payload = payload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "healthy"
        }, new Destination { Name = "orderplaced" }, CancellationToken.None);

        await handler.WaitForMessage(TimeSpan.FromSeconds(5));

        handler.Received.Should().ContainSingle()
            .Which.SourceTransport.Should().Be("healthy");

        await dispatcher.StopAsync(CancellationToken.None);
        await healthyAdapter.DisposeAsync();
    }
}
