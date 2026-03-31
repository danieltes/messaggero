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
/// T064: Aggregate throughput benchmark — two adapters, target ≥10k msg/s (SC-006).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90, iterationCount: 3, warmupCount: 1)]
[MemoryDiagnoser]
public class ThroughputBenchmarks
{
    private MessageBus _bus = null!;
    private InMemoryTransportAdapter _adapter1 = null!;
    private InMemoryTransportAdapter _adapter2 = null!;
    private ThroughputMessage _message = null!;

    public sealed class ThroughputMessage
    {
        public string Id { get; set; } = "perf";
        public int Sequence { get; set; }
    }

    [Params(10_000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _adapter1 = new InMemoryTransportAdapter("transport-a");
        _adapter2 = new InMemoryTransportAdapter("transport-b");
        _adapter1.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        _adapter2.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        var serializer = new JsonMessageSerializer();
        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule
                {
                    MessageType = "ThroughputMessage",
                    Transports = ["transport-a", "transport-b"]
                }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["transport-a"] = new TransportRegistration
                {
                    Name = "transport-a",
                    AdapterFactory = _ => _adapter1,
                    Options = new TransportOptions()
                },
                ["transport-b"] = new TransportRegistration
                {
                    Name = "transport-b",
                    AdapterFactory = _ => _adapter2,
                    Options = new TransportOptions()
                }
            },
            Handlers = [],
            DefaultSerializer = serializer,
            ObservabilityEnabled = false
        };

        var adapters = new Dictionary<string, ITransportAdapter>
        {
            ["transport-a"] = _adapter1,
            ["transport-b"] = _adapter2
        };
        _bus = new MessageBus(config, adapters, NullLogger<MessageBus>.Instance);
        _message = new ThroughputMessage();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _adapter1.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _adapter2.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public async Task<int> PublishBurst()
    {
        for (var i = 0; i < MessageCount; i++)
        {
            _message.Sequence = i;
            await _bus.PublishAsync(_message);
        }
        return MessageCount;
    }
}
