using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Messaggero.Abstractions;
using Messaggero.Concurrency;
using Messaggero.Serialization;

namespace Messaggero.Tests.Performance;

/// <summary>
/// Benchmarks for consume path measuring deserialization, KeyPartitionedProcessor overhead,
/// and end-to-end handler dispatch.
/// Target: ≥ 10k msg/s.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[JsonExporterAttribute.Full]
public class ConsumeBenchmark
{
    private readonly JsonMessageSerializer _serializer = new();
    private byte[] _serializedPayload = null!;
#pragma warning disable CS0649
    private KeyPartitionedProcessor<object>? _processor;
#pragma warning restore CS0649

    [GlobalSetup]
    public void Setup()
    {
        var payload = new TestEvent { Name = "BenchmarkEvent", Value = 42 };
        _serializedPayload = _serializer.Serialize(payload).ToArray();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_processor is not null)
        {
            await _processor.StopAsync();
            await _processor.DisposeAsync();
        }
    }

    [Benchmark(Description = "Deserialize single message")]
    public TestEvent DeserializeSingleMessage()
    {
        return _serializer.Deserialize<TestEvent>(_serializedPayload);
    }

    [Benchmark(Description = "Deserialize 100 messages")]
    [Arguments(100)]
    public void DeserializeBatch(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _serializer.Deserialize<TestEvent>(_serializedPayload);
        }
    }

    [Benchmark(Description = "KeyPartitionedProcessor enqueue (1 partition)")]
    public async Task EnqueueSinglePartition()
    {
        await using var processor = new KeyPartitionedProcessor<object>(1);
        var tcs = new TaskCompletionSource();
        var count = 0;

        for (var i = 0; i < 100; i++)
        {
            await processor.EnqueueAsync("key-1", _ =>
            {
                if (Interlocked.Increment(ref count) == 100)
                    tcs.TrySetResult();
                return Task.CompletedTask;
            });
        }

        await tcs.Task;
    }

    [Benchmark(Description = "KeyPartitionedProcessor enqueue (4 partitions)")]
    public async Task EnqueueFourPartitions()
    {
        await using var processor = new KeyPartitionedProcessor<object>(4);
        var tcs = new TaskCompletionSource();
        var count = 0;

        for (var i = 0; i < 100; i++)
        {
            await processor.EnqueueAsync($"key-{i % 4}", _ =>
            {
                if (Interlocked.Increment(ref count) == 100)
                    tcs.TrySetResult();
                return Task.CompletedTask;
            });
        }

        await tcs.Task;
    }

    [Benchmark(Description = "Full consume pipeline (deserialize + dispatch)")]
    public void FullConsumePipeline()
    {
        var metadata = new MessageMetadata
        {
            MessageId = "bench-001",
            ContentType = "application/json"
        };

        var payload = _serializer.Deserialize<TestEvent>(_serializedPayload);
        var envelope = new MessageEnvelope<TestEvent>
        {
            MessageId = metadata.MessageId,
            Payload = payload,
            Destination = "bench.topic",
            Timestamp = metadata.Timestamp,
            ContentType = metadata.ContentType
        };

        // Simulate handler invocation (sync for benchmark purity)
        _ = envelope.Payload.Name;
    }

    public record TestEvent
    {
        public string Name { get; init; } = string.Empty;
        public int Value { get; init; }
    }
}
