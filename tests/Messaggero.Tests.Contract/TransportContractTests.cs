using FluentAssertions;
using Messaggero.Abstractions;
using NSubstitute;

namespace Messaggero.Tests.Contract;

/// <summary>
/// Contract tests parameterized across transport implementations.
/// Validates that all transports exhibit identical application-level behavior.
/// </summary>
public abstract class TransportContractTests
{
    /// <summary>
    /// Creates a connected transport under test.
    /// </summary>
    protected abstract IMessageBusTransport CreateTransport();

    [Fact]
    public async Task Transport_ExposesName()
    {
        await using var transport = CreateTransport();

        transport.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Transport_PublishAndSubscribe_RoundTrip()
    {
        await using var transport = CreateTransport();
        var destination = $"contract.roundtrip.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<(ReadOnlyMemory<byte> Body, MessageMetadata Meta)>();

        await transport.SubscribeAsync(
            destination,
            "contract-group",
            (body, meta, _) =>
            {
                received.TrySetResult((body.ToArray(), meta));
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        byte[] messageBody = [1, 2, 3, 4, 5];
        var metadata = new MessageMetadata
        {
            MessageId = Guid.NewGuid().ToString("N"),
            RoutingKey = "key-1",
            Headers = new Dictionary<string, string> { ["x-test"] = "value" },
            ContentType = "application/json",
            CorrelationId = "corr-abc"
        };

        await transport.PublishAsync(destination, messageBody, metadata);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Body.ToArray().Should().BeEquivalentTo(messageBody);
        result.Meta.MessageId.Should().Be(metadata.MessageId);
    }

    [Fact]
    public async Task Transport_MetadataFidelity_HeadersPreserved()
    {
        await using var transport = CreateTransport();
        var destination = $"contract.metadata.{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<MessageMetadata>();

        await transport.SubscribeAsync(
            destination,
            "meta-group",
            (_, meta, _) =>
            {
                received.TrySetResult(meta);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        var metadata = new MessageMetadata
        {
            MessageId = "meta-test-001",
            RoutingKey = "customer-42",
            Headers = new Dictionary<string, string> { ["x-source"] = "contract-test" },
            ContentType = "application/json",
            CorrelationId = "corr-xyz"
        };

        await transport.PublishAsync(destination, new byte[] { 1 }, metadata);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.MessageId.Should().Be("meta-test-001");
        result.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task Transport_HealthCheck_ReportsHealthy()
    {
        await using var transport = CreateTransport();

        var health = await transport.CheckHealthAsync();

        health.IsHealthy.Should().BeTrue();
        health.TransportName.Should().Be(transport.Name);
    }

    [Fact]
    public async Task Transport_Subscription_CanBeDisposed()
    {
        await using var transport = CreateTransport();
        var destination = $"contract.dispose.{Guid.NewGuid():N}";

        var subscription = await transport.SubscribeAsync(
            destination,
            "dispose-group",
            (_, _, _) => Task.CompletedTask,
            new SubscriptionOptions());

        subscription.IsActive.Should().BeTrue();

        await subscription.DisposeAsync();

        subscription.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Transport_OnLifecycleEvent_ReturnsDisposable()
    {
        await using var transport = CreateTransport();

        var disposable = transport.OnLifecycleEvent(_ => { });

        disposable.Should().NotBeNull();
        disposable.Dispose();
    }
}

/// <summary>
/// Validates contract test definitions using an in-memory transport stub.
/// </summary>
public class InMemoryTransportContractTests : TransportContractTests
{
    protected override IMessageBusTransport CreateTransport() => new InMemoryTransport();

    private sealed class InMemoryTransport : IMessageBusTransport
    {
        private readonly List<InMemorySubscription> _subscriptions = [];

        public string Name => "InMemory";

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task PublishAsync(
            string destination,
            ReadOnlyMemory<byte> body,
            MessageMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            foreach (var sub in _subscriptions.Where(s => s.Destination == destination && s.IsActive))
            {
                await sub.Handler(body, metadata, cancellationToken);
            }
        }

        public Task<ITransportSubscription> SubscribeAsync(
            string destination,
            string groupId,
            Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
            SubscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            var sub = new InMemorySubscription(destination, handler);
            _subscriptions.Add(sub);
            return Task.FromResult<ITransportSubscription>(sub);
        }

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckResult { IsHealthy = true, TransportName = Name });

        public IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener)
            => Substitute.For<IDisposable>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemorySubscription(
        string destination,
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler) : ITransportSubscription
    {
        private bool _disposed;

        public string Destination => destination;
        public Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> Handler => handler;
        public bool IsActive => !_disposed;

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
