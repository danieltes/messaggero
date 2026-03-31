using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Messaggero.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero.Tests.Benchmarks;

/// <summary>
/// T063: Publish overhead benchmark — payloads ≤1MB, target ≤1ms p99 (SC-005).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class PublishBenchmarks
{
    private MessageBus _bus = null!;
    private InMemoryTransportAdapter _adapter = null!;
    private SmallMessage _smallMessage = null!;
    private LargeMessage _largeMessage = null!;

    public sealed class SmallMessage
    {
        public string Id { get; set; } = "msg-1";
        public int Value { get; set; } = 42;
    }

    public sealed class LargeMessage
    {
        public string Id { get; set; } = "msg-large";
        public byte[] Payload { get; set; } = null!;
    }

    [GlobalSetup]
    public void Setup()
    {
        _adapter = new InMemoryTransportAdapter("bench-transport");
        _adapter.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = "SmallMessage", Transports = ["bench-transport"] },
                new RoutingRule { MessageType = "LargeMessage", Transports = ["bench-transport"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["bench-transport"] = new TransportRegistration
                {
                    Name = "bench-transport",
                    AdapterFactory = _ => _adapter,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer,
            ObservabilityEnabled = false
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["bench-transport"] = _adapter };
        _bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);

        _smallMessage = new SmallMessage();
        _largeMessage = new LargeMessage { Payload = new byte[1_000_000] }; // ~1MB
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task<PublishResult> PublishSmall() =>
        _bus.PublishAsync(_smallMessage);

    [Benchmark]
    public Task<PublishResult> PublishLarge1MB() =>
        _bus.PublishAsync(_largeMessage);
}
