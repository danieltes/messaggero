using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using Messaggero.Tests.Integration.Fixtures;
using Messaggero.Transport.RabbitMQ;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero.Tests.Integration;

[Collection("RabbitMQ")]
public class RabbitMqIntegrationTests : IAsyncDisposable
{
    private readonly RabbitMqFixture _fixture;
    private readonly JsonMessageSerializer _serializer = new();
    private RabbitMqTransport? _transport;

    public RabbitMqIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_MessageArrivesOnQueue()
    {
        var transport = await CreateAndConnectTransportAsync();
        var destination = $"test.publish.{Guid.NewGuid():N}";
        var payload = new TestOrder { OrderId = Guid.NewGuid(), Total = 42.50m };
        var body = _serializer.Serialize(payload);
        var metadata = CreateMetadata();

        await transport.PublishAsync(destination, body, metadata);

        // Subscribe and verify
        var received = new TaskCompletionSource<Abstractions.MessageMetadata>();
        await transport.SubscribeAsync(
            destination,
            "test-group",
            (_, meta, _) =>
            {
                received.TrySetResult(meta);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        result.MessageId.Should().Be(metadata.MessageId);
    }

    [Fact]
    public async Task PublishAsync_MetadataFidelity_HeadersPreserved()
    {
        var transport = await CreateAndConnectTransportAsync();
        var destination = $"test.headers.{Guid.NewGuid():N}";
        var body = _serializer.Serialize("test");
        var metadata = new Abstractions.MessageMetadata
        {
            MessageId = Guid.NewGuid().ToString("N"),
            RoutingKey = "customer-42",
            Headers = new Dictionary<string, string>
            {
                ["x-trace-id"] = "trace-abc",
                ["x-source"] = "integration-test"
            },
            ContentType = "application/json",
            CorrelationId = "corr-123"
        };

        await transport.PublishAsync(destination, body, metadata);

        var received = new TaskCompletionSource<Abstractions.MessageMetadata>();
        await transport.SubscribeAsync(
            destination,
            "test-group-headers",
            (_, meta, _) =>
            {
                received.TrySetResult(meta);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        result.MessageId.Should().Be(metadata.MessageId);
        result.CorrelationId.Should().Be("corr-123");
        result.Headers.Should().ContainKey("x-trace-id");
        result.Headers["x-trace-id"].Should().Be("trace-abc");
    }

    [Fact]
    public async Task PublishAsync_PayloadRoundTrip()
    {
        var transport = await CreateAndConnectTransportAsync();
        var destination = $"test.roundtrip.{Guid.NewGuid():N}";
        var original = new TestOrder { OrderId = Guid.NewGuid(), Total = 99.99m };
        var body = _serializer.Serialize(original);
        var metadata = CreateMetadata();

        await transport.PublishAsync(destination, body, metadata);

        var receivedBody = new TaskCompletionSource<ReadOnlyMemory<byte>>();
        await transport.SubscribeAsync(
            destination,
            "test-group-roundtrip",
            (b, _, _) =>
            {
                receivedBody.TrySetResult(b.ToArray());
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        var result = await receivedBody.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var deserialized = _serializer.Deserialize<TestOrder>(result.Span);
        deserialized.OrderId.Should().Be(original.OrderId);
        deserialized.Total.Should().Be(original.Total);
    }

    [Fact]
    public async Task HealthCheck_WhenConnected_ReportsHealthy()
    {
        var transport = await CreateAndConnectTransportAsync();

        var health = await transport.CheckHealthAsync();

        health.IsHealthy.Should().BeTrue();
        health.TransportName.Should().Be("RabbitMQ");
    }

    [Fact]
    public async Task SubscribeAsync_CompetingConsumers_OneReceives()
    {
        var transport = await CreateAndConnectTransportAsync();
        var destination = $"test.competing.{Guid.NewGuid():N}";
        var received1 = new TaskCompletionSource<bool>();
        var received2 = new TaskCompletionSource<bool>();

        // Two subscribers in the same group
        await transport.SubscribeAsync(destination, "same-group",
            (_, _, _) => { received1.TrySetResult(true); return Task.CompletedTask; },
            new SubscriptionOptions());

        await transport.SubscribeAsync(destination, "same-group",
            (_, _, _) => { received2.TrySetResult(true); return Task.CompletedTask; },
            new SubscriptionOptions());

        var body = _serializer.Serialize("test");
        var metadata = CreateMetadata();
        await transport.PublishAsync(destination, body, metadata);

        // At least one should receive (competing consumers — only one gets it)
        var completedTask = await Task.WhenAny(
            received1.Task.WaitAsync(TimeSpan.FromSeconds(10)),
            received2.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        completedTask.Result.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeAsync_GracefulShutdown_DisposesCleanly()
    {
        var transport = await CreateAndConnectTransportAsync();
        var destination = $"test.shutdown.{Guid.NewGuid():N}";

        var subscription = await transport.SubscribeAsync(destination, "shutdown-group",
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

    private async Task<RabbitMqTransport> CreateAndConnectTransportAsync()
    {
        var config = new RabbitMqConfiguration
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = "guest",
            Password = "guest"
        };

        _transport = new RabbitMqTransport(config, NullLogger<RabbitMqTransport>.Instance);
        await _transport.ConnectAsync();
        return _transport;
    }

    private static Abstractions.MessageMetadata CreateMetadata() => new()
    {
        MessageId = Guid.NewGuid().ToString("N"),
        ContentType = "application/json"
    };

    public record TestOrder
    {
        public Guid OrderId { get; init; }
        public decimal Total { get; init; }
    }
}
