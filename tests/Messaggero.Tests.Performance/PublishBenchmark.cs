using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using NSubstitute;

namespace Messaggero.Tests.Performance;

/// <summary>
/// Benchmarks for publish path measuring throughput, latency, and memory allocations.
/// Target: ≥ 10k msg/s.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
public class PublishBenchmark
{
    private MessageBus _bus = null!;
    private IMessageBusTransport _transport = null!;
    private readonly JsonMessageSerializer _serializer = new();
    private TestOrder _payload = null!;

    [GlobalSetup]
    public void Setup()
    {
        _transport = Substitute.For<IMessageBusTransport>();
        _transport.Name.Returns("Benchmark");
        _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<MessageMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _bus = new MessageBus(_transport, _serializer);
        _payload = new TestOrder { OrderId = Guid.NewGuid(), Total = 99.99m, Description = "Benchmark order" };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _bus.DisposeAsync();
    }

    [Benchmark(Description = "Publish single message")]
    public Task PublishSingleMessage()
    {
        return _bus.PublishAsync("benchmark.orders", _payload);
    }

    [Benchmark(Description = "Publish with options")]
    public Task PublishWithOptions()
    {
        return _bus.PublishAsync("benchmark.orders", _payload, new MessagePublishOptions
        {
            RoutingKey = "customer-42",
            CorrelationId = "corr-bench",
            Headers = new Dictionary<string, string> { ["x-trace"] = "bench" }
        });
    }

    [Benchmark(Description = "Publish 100 messages")]
    [Arguments(100)]
    public async Task PublishBatch(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _bus.PublishAsync("benchmark.orders", _payload);
        }
    }

    [Benchmark(Description = "Serialize only")]
    public ReadOnlyMemory<byte> SerializeOnly()
    {
        return _serializer.Serialize(_payload);
    }

    public record TestOrder
    {
        public Guid OrderId { get; init; }
        public decimal Total { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}
