using System.Text.Json;
using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using NSubstitute;

namespace Messaggero.Tests.Unit;

public class LifecycleEventEmissionTests : IAsyncDisposable
{
    private readonly IMessageBusTransport _transport = Substitute.For<IMessageBusTransport>();
    private readonly JsonMessageSerializer _serializer = new();
    private readonly MessageBus _bus;
    private readonly List<LifecycleEvent> _events = [];

    public LifecycleEventEmissionTests()
    {
        _transport.Name.Returns("Test");
        _bus = new MessageBus(_transport, _serializer);

        // Capture listener registrations
        _transport.OnLifecycleEvent(Arg.Any<Action<LifecycleEvent>>())
            .Returns(ci =>
            {
                var listener = ci.Arg<Action<LifecycleEvent>>();
                return Substitute.For<IDisposable>();
            });
    }

    [Fact]
    public void OnLifecycleEvent_RegistersListener()
    {
        var received = new List<LifecycleEvent>();
        var disposable = _bus.OnLifecycleEvent(e => received.Add(e));

        disposable.Should().NotBeNull();
    }

    [Fact]
    public void OnLifecycleEvent_WithNullListener_Throws()
    {
        var act = () => _bus.OnLifecycleEvent(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_DelegatesToTransport()
    {
        _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<MessageMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _bus.PublishAsync("test.destination", "hello");

        await _transport.Received(1).PublishAsync(
            "test.destination",
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Is<MessageMetadata>(m => m.ContentType == "application/json"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_DeserializesAndDispatchesHandler()
    {
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>? capturedCallback = null;

        _transport.SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(cb => capturedCallback = cb),
            Arg.Any<SubscriptionOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());

        var handler = Substitute.For<IMessageHandler<string>>();
        await _bus.SubscribeAsync("events", "my-group", handler);

        capturedCallback.Should().NotBeNull();

        var payload = JsonSerializer.SerializeToUtf8Bytes("test-payload");
        var metadata = new MessageMetadata
        {
            MessageId = "msg-123",
            ContentType = "application/json"
        };

        await capturedCallback!(payload, metadata, CancellationToken.None);

        await handler.Received(1).HandleAsync(
            Arg.Is<MessageEnvelope<string>>(e =>
                e.MessageId == "msg-123" &&
                e.Payload == "test-payload"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_DelegatesToTransport()
    {
        var transportResult = new HealthCheckResult
        {
            IsHealthy = true,
            TransportName = "Test",
            Description = "OK"
        };
        _transport.Name.Returns("Test");
        _transport.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(transportResult));

        var result = await _bus.CheckHealthAsync();

        result.IsHealthy.Should().BeTrue();
        result.TransportName.Should().Be("Test");
        result.TransportEntries.Should().HaveCount(1);
        result.TransportEntries[0].TransportName.Should().Be("Test");
        result.TransportEntries[0].IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void LifecycleEvent_HasCorrectProperties()
    {
        var evt = new LifecycleEvent
        {
            EventType = LifecycleEventType.MessagePublished,
            TransportName = "RabbitMQ",
            Destination = "orders",
            MessageId = "msg-001",
            Timestamp = DateTimeOffset.UtcNow
        };

        evt.EventType.Should().Be(LifecycleEventType.MessagePublished);
        evt.TransportName.Should().Be("RabbitMQ");
        evt.Destination.Should().Be("orders");
        evt.MessageId.Should().Be("msg-001");
        evt.Error.Should().BeNull();
    }

    [Fact]
    public void LifecycleEvent_WithError_HasErrorProperty()
    {
        var exception = new InvalidOperationException("Connection lost");
        var evt = new LifecycleEvent
        {
            EventType = LifecycleEventType.TransportFailed,
            TransportName = "Kafka",
            Error = exception
        };

        evt.Error.Should().BeSameAs(exception);
    }

    [Fact]
    public void LifecycleEventType_HasAllRequiredValues()
    {
        var values = Enum.GetValues<LifecycleEventType>();

        values.Should().Contain(LifecycleEventType.TransportConnected);
        values.Should().Contain(LifecycleEventType.TransportDisconnected);
        values.Should().Contain(LifecycleEventType.TransportReconnecting);
        values.Should().Contain(LifecycleEventType.TransportFailed);
        values.Should().Contain(LifecycleEventType.MessagePublished);
        values.Should().Contain(LifecycleEventType.MessageReceived);
        values.Should().Contain(LifecycleEventType.MessageError);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _bus.DisposeAsync();
    }
}
