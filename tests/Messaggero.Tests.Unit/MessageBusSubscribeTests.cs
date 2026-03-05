using System.Text.Json;
using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using NSubstitute;

namespace Messaggero.Tests.Unit;

public class MessageBusSubscribeTests : IAsyncDisposable
{
    private readonly IMessageBusTransport _transport = Substitute.For<IMessageBusTransport>();
    private readonly IMessageSerializer _serializer = Substitute.For<IMessageSerializer>();
    private readonly MessageBus _bus;

    public MessageBusSubscribeTests()
    {
        _serializer.ContentType.Returns("application/json");
        _bus = new MessageBus(_transport, _serializer);
    }

    [Fact]
    public async Task SubscribeAsync_RegistersWithTransport()
    {
        var handler = Substitute.For<IMessageHandler<string>>();
        var subscription = Substitute.For<ITransportSubscription>();
        subscription.IsActive.Returns(true);

        _transport.SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(subscription);

        var handle = await _bus.SubscribeAsync("orders.created", "my-group", handler);

        handle.Should().NotBeNull();
        handle.Destination.Should().Be("orders.created");
        handle.GroupId.Should().Be("my-group");
        handle.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeAsync_DeserializesPayloadAndConstructsEnvelope()
    {
        // Use a real serializer because NSubstitute cannot proxy ReadOnlySpan<byte> parameters
        var realSerializer = new JsonMessageSerializer();
        var bus = new MessageBus(_transport, realSerializer);
        var handler = Substitute.For<IMessageHandler<string>>();
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>? capturedCallback = null;

        _transport.SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(cb => capturedCallback = cb),
            Arg.Any<SubscriptionOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());

        await bus.SubscribeAsync("dest", "group", handler);

        capturedCallback.Should().NotBeNull();

        var payload = JsonSerializer.SerializeToUtf8Bytes("hello-world");
        var metadata = new MessageMetadata
        {
            MessageId = "msg-001",
            RoutingKey = "key-1",
            ContentType = "application/json",
            CorrelationId = "corr-1"
        };

        await capturedCallback!(payload, metadata, CancellationToken.None);

        await handler.Received(1).HandleAsync(
            Arg.Is<MessageEnvelope<string>>(e =>
                e.MessageId == "msg-001" &&
                e.Payload == "hello-world" &&
                e.Destination == "dest" &&
                e.RoutingKey == "key-1" &&
                e.CorrelationId == "corr-1"),
            Arg.Any<CancellationToken>());

        await bus.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_WithNullDestination_Throws()
    {
        var handler = Substitute.For<IMessageHandler<string>>();

        var act = () => _bus.SubscribeAsync(null!, "group", handler);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SubscribeAsync_WithNullGroupId_Throws()
    {
        var handler = Substitute.For<IMessageHandler<string>>();

        var act = () => _bus.SubscribeAsync("dest", null!, handler);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SubscribeAsync_WithNullHandler_Throws()
    {
        var act = () => _bus.SubscribeAsync<string>("dest", "group", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SubscribeAsync_DefaultSubscriptionOptions_UsedWhenNoneProvided()
    {
        var handler = Substitute.For<IMessageHandler<string>>();

        _transport.SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());

        await _bus.SubscribeAsync("dest", "group", handler);

        await _transport.Received(1).SubscribeAsync(
            "dest",
            "group",
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Is<SubscriptionOptions>(o => o.MaxConcurrency == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllSubscriptions()
    {
        var handler = Substitute.For<IMessageHandler<string>>();
        var subscription = Substitute.For<ITransportSubscription>();

        _transport.SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(subscription);

        await _bus.SubscribeAsync("dest1", "group", handler);
        await _bus.SubscribeAsync("dest2", "group", handler);

        await _bus.DisposeAsync();

        // Subscriptions should have been disposed
        await subscription.Received(2).DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var handler = Substitute.For<IMessageHandler<string>>();
        await _bus.DisposeAsync();

        var act = () => _bus.SubscribeAsync("dest", "group", handler);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _bus.DisposeAsync();
    }
}
