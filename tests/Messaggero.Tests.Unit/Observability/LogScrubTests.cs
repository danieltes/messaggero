using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Messaggero.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Messaggero.Tests.Unit.Observability;

/// <summary>
/// T061: Automated log-scrubbing — verify no payloads, credentials, or tokens in log output (SC-010).
/// </summary>
public class LogScrubTests
{
    private sealed record OrderPlaced(string OrderId, decimal Amount, string Secret);

    [Fact]
    public async Task PublishLogs_DoNotContainPayloadOrSecrets()
    {
        var logSink = new CapturingLogger();
        var adapter = new InMemoryTransportAdapter("test-transport");
        await adapter.StartAsync(CancellationToken.None);

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "OrderPlaced", Transports = ["test-transport"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["test-transport"] = new TransportRegistration
                {
                    Name = "test-transport",
                    AdapterFactory = _ => adapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer,
            ObservabilityEnabled = true
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["test-transport"] = adapter };
        var bus = new MessageBus(config, adapters, logSink);

        var order = new OrderPlaced("ORD-SECRET-123", 9999.99m, "super-secret-api-key-xyz");
        await bus.PublishAsync(order);

        var allLogs = string.Join("\n", logSink.Messages);

        // Payload content must never appear in logs
        allLogs.Should().NotContain("super-secret-api-key-xyz", "credentials must not be logged");
        allLogs.Should().NotContain("9999.99", "payload data must not be logged");
        allLogs.Should().NotContain("ORD-SECRET-123", "payload data must not be logged");

        // But message type and transport should be logged for diagnostics
        allLogs.Should().Contain("OrderPlaced");
        allLogs.Should().Contain("test-transport");

        await adapter.DisposeAsync();
    }

    /// <summary>
    /// Captures structured log output for inspection.
    /// </summary>
    private sealed class CapturingLogger : ILogger<MessageBus>
    {
        public List<string> Messages { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
