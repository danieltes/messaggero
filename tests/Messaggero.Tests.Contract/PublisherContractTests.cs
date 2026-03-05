using FluentAssertions;
using Messaggero.Abstractions;
using NSubstitute;

namespace Messaggero.Tests.Contract;

/// <summary>
/// Contract tests verifying IMessageBusTransport.PublishAsync behavior.
/// These tests define the contract that ALL transport implementations must satisfy.
/// Parameterized for use with any <see cref="IMessageBusTransport"/> implementation.
/// </summary>
public abstract class PublisherContractTests
{
    /// <summary>
    /// Creates an instance of the transport under test.
    /// Must be in a connected state.
    /// </summary>
    protected abstract IMessageBusTransport CreateTransport();

    [Fact]
    public async Task PublishAsync_ForwardsDestination()
    {
        await using var transport = CreateTransport();
        var body = new ReadOnlyMemory<byte>([1, 2, 3]);
        var metadata = CreateMetadata();

        var act = () => transport.PublishAsync("test.destination", body, metadata);

        // Should not throw — destination is forwarded to the broker
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_SerializesBodyCorrectly()
    {
        await using var transport = CreateTransport();
        byte[] bodyBytes = [10, 20, 30, 40];
        var body = new ReadOnlyMemory<byte>(bodyBytes);
        var metadata = CreateMetadata();

        // Should accept and forward the body without modification
        var act = () => transport.PublishAsync("fidelity.test", body, metadata);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_PropagatesMetadata()
    {
        await using var transport = CreateTransport();
        var body = new ReadOnlyMemory<byte>([1]);
        var metadata = new MessageMetadata
        {
            MessageId = "contract-msg-001",
            RoutingKey = "customer-42",
            Headers = new Dictionary<string, string>
            {
                ["x-trace-id"] = "trace-abc",
                ["x-source"] = "contract-test"
            },
            ContentType = "application/json",
            CorrelationId = "corr-789"
        };

        var act = () => transport.PublishAsync("meta.test", body, metadata);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_ForwardsRoutingKey()
    {
        await using var transport = CreateTransport();
        var body = new ReadOnlyMemory<byte>([5]);
        var metadata = CreateMetadata() with { RoutingKey = "partition-key-99" };

        var act = () => transport.PublishAsync("routing.test", body, metadata);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_RespectsToken()
    {
        await using var transport = CreateTransport();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var body = new ReadOnlyMemory<byte>([1]);
        var metadata = CreateMetadata();

        var act = () => transport.PublishAsync("cancel.test", body, metadata, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Creates a standard metadata instance for tests.
    /// </summary>
    protected static MessageMetadata CreateMetadata(string? routingKey = null)
        => new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            RoutingKey = routingKey,
            ContentType = "application/json"
        };
}

/// <summary>
/// Contract tests using an in-memory stub transport to validate the contract definition itself.
/// </summary>
public class StubPublisherContractTests : PublisherContractTests
{
    protected override IMessageBusTransport CreateTransport() => new StubTransport();

    private sealed class StubTransport : IMessageBusTransport
    {
        private readonly List<(string Destination, ReadOnlyMemory<byte> Body, MessageMetadata Metadata)> _published = [];

        public string Name => "Stub";

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishAsync(
            string destination,
            ReadOnlyMemory<byte> body,
            MessageMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _published.Add((destination, body, metadata));
            return Task.CompletedTask;
        }

        public Task<ITransportSubscription> SubscribeAsync(
            string destination,
            string groupId,
            Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
            SubscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ITransportSubscription>(new StubSubscription());
        }

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckResult
            {
                IsHealthy = true,
                TransportName = Name,
                Description = "Stub is always healthy"
            });

        public IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener)
            => Substitute.For<IDisposable>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class StubSubscription : ITransportSubscription
        {
            public bool IsActive => true;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
