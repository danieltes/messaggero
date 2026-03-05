using FluentAssertions;
using Messaggero.Abstractions;
using NSubstitute;

namespace Messaggero.Tests.Contract;

/// <summary>
/// Contract tests verifying IMessageBusTransport.SubscribeAsync behavior.
/// These tests define the contract that ALL transport implementations must satisfy.
/// </summary>
public abstract class SubscriberContractTests
{
    /// <summary>
    /// Creates a connected transport under test.
    /// </summary>
    protected abstract IMessageBusTransport CreateTransport();

    [Fact]
    public async Task SubscribeAsync_ReturnsActiveSubscription()
    {
        await using var transport = CreateTransport();

        var subscription = await transport.SubscribeAsync(
            "test.destination",
            "test-group",
            (_, _, _) => Task.CompletedTask,
            new SubscriptionOptions());

        subscription.Should().NotBeNull();
        subscription.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeAsync_InvokesHandlerOnMessage()
    {
        await using var transport = CreateTransport();
        var received = new TaskCompletionSource<bool>();

        await transport.SubscribeAsync(
            "invoke.test",
            "test-group",
            (_, _, _) =>
            {
                received.TrySetResult(true);
                return Task.CompletedTask;
            },
            new SubscriptionOptions());

        // Publish a message for the handler to receive
        var metadata = new MessageMetadata
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ContentType = "application/json"
        };
        await transport.PublishAsync("invoke.test", new byte[] { 1 }, metadata);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeAsync_Dispose_StopsSubscription()
    {
        await using var transport = CreateTransport();

        var subscription = await transport.SubscribeAsync(
            "dispose.test",
            "test-group",
            (_, _, _) => Task.CompletedTask,
            new SubscriptionOptions());

        await subscription.DisposeAsync();

        subscription.IsActive.Should().BeFalse();
    }
}

/// <summary>
/// Contract tests using an in-memory stub transport.
/// </summary>
public class StubSubscriberContractTests : SubscriberContractTests
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
