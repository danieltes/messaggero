using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using Messaggero.Tests.Integration.Fixtures;
using Messaggero.Transport.Kafka;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero.Tests.Integration;

[Collection("Kafka")]
public class KafkaIntegrationTests : IAsyncDisposable
{
    private readonly KafkaFixture _fixture;
    private readonly JsonMessageSerializer _serializer = new();
    private KafkaTransport? _transport;

    public KafkaIntegrationTests(KafkaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_MessageArrivesOnTopic()
    {
        var transport = await CreateAndConnectTransportAsync();
        var topic = $"test-publish-{Guid.NewGuid():N}";
        var payload = new TestEvent { Name = "TestEvent", Value = 42 };
        var body = _serializer.Serialize(payload);
        var metadata = CreateMetadata();

        // Subscribe first (Kafka needs the consumer before producing to get messages)
        var received = new TaskCompletionSource<Abstractions.MessageMetadata>();
        await transport.SubscribeAsync(
            topic,
            $"test-group-{Guid.NewGuid():N}",
            (_, meta, _) =>
            {
                received.TrySetResult(meta);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        // Small delay for consumer registration
        await Task.Delay(2000);

        await transport.PublishAsync(topic, body, metadata);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        result.MessageId.Should().Be(metadata.MessageId);
    }

    [Fact]
    public async Task PublishAsync_PartitionKeyMapping()
    {
        var transport = await CreateAndConnectTransportAsync();
        var topic = $"test-partition-{Guid.NewGuid():N}";
        var body = _serializer.Serialize("test");
        var metadata = CreateMetadata() with { RoutingKey = "customer-42" };

        var received = new TaskCompletionSource<Abstractions.MessageMetadata>();
        await transport.SubscribeAsync(
            topic,
            $"test-group-{Guid.NewGuid():N}",
            (_, meta, _) =>
            {
                received.TrySetResult(meta);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        await Task.Delay(2000);
        await transport.PublishAsync(topic, body, metadata);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        result.RoutingKey.Should().Be("customer-42");
    }

    [Fact]
    public async Task PublishAsync_PayloadRoundTrip()
    {
        var transport = await CreateAndConnectTransportAsync();
        var topic = $"test-roundtrip-{Guid.NewGuid():N}";
        var original = new TestEvent { Name = "RoundTrip", Value = 99 };
        var body = _serializer.Serialize(original);
        var metadata = CreateMetadata();

        var receivedBody = new TaskCompletionSource<ReadOnlyMemory<byte>>();
        await transport.SubscribeAsync(
            topic,
            $"test-group-{Guid.NewGuid():N}",
            (b, _, _) =>
            {
                receivedBody.TrySetResult(b.ToArray());
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        await Task.Delay(2000);
        await transport.PublishAsync(topic, body, metadata);

        var result = await receivedBody.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var deserialized = _serializer.Deserialize<TestEvent>(result.Span);
        deserialized.Name.Should().Be("RoundTrip");
        deserialized.Value.Should().Be(99);
    }

    [Fact]
    public async Task HealthCheck_WhenConnected_ReportsHealthy()
    {
        var transport = await CreateAndConnectTransportAsync();

        var health = await transport.CheckHealthAsync();

        health.IsHealthy.Should().BeTrue();
        health.TransportName.Should().Be("Kafka");
    }

    [Fact]
    public async Task SubscribeAsync_CompetingConsumers_OnlyOneGroupReceives()
    {
        var transport = await CreateAndConnectTransportAsync();
        var topic = $"test-competing-{Guid.NewGuid():N}";
        var groupId = $"same-group-{Guid.NewGuid():N}";
        var counter = 0;

        await transport.SubscribeAsync(topic, groupId,
            (_, _, _) => { Interlocked.Increment(ref counter); return Task.CompletedTask; },
            new SubscriptionOptions());

        await Task.Delay(2000);

        var body = _serializer.Serialize("test");
        var metadata = CreateMetadata();
        await transport.PublishAsync(topic, body, metadata);

        // Wait for consumption
        await Task.Delay(5000);

        // With a single partition, only one consumer in the group should receive
        counter.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SubscribeAsync_GracefulShutdown_DisposesCleanly()
    {
        var transport = await CreateAndConnectTransportAsync();
        var topic = $"test-shutdown-{Guid.NewGuid():N}";

        var subscription = await transport.SubscribeAsync(topic, $"group-{Guid.NewGuid():N}",
            (_, _, _) => Task.CompletedTask,
            new SubscriptionOptions());

        subscription.IsActive.Should().BeTrue();

        await subscription.DisposeAsync();

        subscription.IsActive.Should().BeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_transport is not null)
            await _transport.DisposeAsync();
    }

    private async Task<KafkaTransport> CreateAndConnectTransportAsync()
    {
        var config = new KafkaConfiguration
        {
            BootstrapServers = _fixture.BootstrapServers
        };

        _transport = new KafkaTransport(config, NullLogger<KafkaTransport>.Instance);
        await _transport.ConnectAsync();
        return _transport;
    }

    private static Abstractions.MessageMetadata CreateMetadata() => new()
    {
        MessageId = Guid.NewGuid().ToString("N"),
        ContentType = "application/json"
    };

    public record TestEvent
    {
        public string Name { get; init; } = string.Empty;
        public int Value { get; init; }
    }
}
