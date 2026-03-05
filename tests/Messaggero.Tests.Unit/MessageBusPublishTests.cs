using FluentAssertions;
using Messaggero.Abstractions;
using NSubstitute;

namespace Messaggero.Tests.Unit;

public class MessageBusPublishTests : IAsyncDisposable
{
    private readonly IMessageBusTransport _transport = Substitute.For<IMessageBusTransport>();
    private readonly IMessageSerializer _serializer = Substitute.For<IMessageSerializer>();
    private readonly MessageBus _bus;

    public MessageBusPublishTests()
    {
        _serializer.ContentType.Returns("application/json");
        _bus = new MessageBus(_transport, _serializer);
    }

    [Fact]
    public async Task PublishAsync_SerializesPayloadViaSerializer()
    {
        var payload = new TestPayload { Value = "hello" };
        byte[] expectedBytes = [1, 2, 3];
        _serializer.Serialize(payload).Returns(expectedBytes);

        await _bus.PublishAsync("orders.created", payload);

        _serializer.Received(1).Serialize(payload);
    }

    [Fact]
    public async Task PublishAsync_DelegatesToTransportPublishAsync()
    {
        var payload = "test";
        byte[] serialized = [10, 20];
        _serializer.Serialize(payload).Returns(serialized);

        await _bus.PublishAsync("events.topic", payload);

        await _transport.Received(1).PublishAsync(
            "events.topic",
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<MessageMetadata>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_SetsMessageIdOnMetadata()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _bus.PublishAsync("dest", "payload");

        captured.Should().NotBeNull();
        captured!.MessageId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PublishAsync_SetsTimestampOnMetadata()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        var before = DateTimeOffset.UtcNow;
        await _bus.PublishAsync("dest", "payload");
        var after = DateTimeOffset.UtcNow;

        captured!.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task PublishAsync_SetsContentTypeFromSerializer()
    {
        _serializer.ContentType.Returns("application/xml");
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        var bus = new MessageBus(_transport, _serializer);
        await bus.PublishAsync("dest", "payload");

        captured!.ContentType.Should().Be("application/xml");
    }

    [Fact]
    public async Task PublishAsync_ForwardsRoutingKeyFromOptions()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        var options = new MessagePublishOptions { RoutingKey = "customer-123" };
        await _bus.PublishAsync("dest", "payload", options);

        captured!.RoutingKey.Should().Be("customer-123");
    }

    [Fact]
    public async Task PublishAsync_ForwardsHeadersFromOptions()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        var options = new MessagePublishOptions
        {
            Headers = new Dictionary<string, string> { ["trace-id"] = "abc123" }
        };
        await _bus.PublishAsync("dest", "payload", options);

        captured!.Headers.Should().ContainKey("trace-id").WhoseValue.Should().Be("abc123");
    }

    [Fact]
    public async Task PublishAsync_ForwardsCorrelationIdFromOptions()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        var options = new MessagePublishOptions { CorrelationId = "corr-456" };
        await _bus.PublishAsync("dest", "payload", options);

        captured!.CorrelationId.Should().Be("corr-456");
    }

    [Fact]
    public async Task PublishAsync_WithNullDestination_ThrowsArgumentException()
    {
        var act = () => _bus.PublishAsync(null!, "payload");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PublishAsync_WithEmptyDestination_ThrowsArgumentException()
    {
        var act = () => _bus.PublishAsync("", "payload");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PublishAsync_WithWhitespaceDestination_ThrowsArgumentException()
    {
        var act = () => _bus.PublishAsync("   ", "payload");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PublishAsync_ForwardsCancellationToken()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await _bus.PublishAsync("dest", "payload", cancellationToken: token);

        await _transport.Received(1).PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<MessageMetadata>(),
            token);
    }

    [Fact]
    public async Task PublishAsync_WithNoOptions_UsesEmptyHeadersAndNullRoutingKey()
    {
        _serializer.Serialize(Arg.Any<string>()).Returns([1]);
        MessageMetadata? captured = null;

        await _transport.PublishAsync(
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Do<MessageMetadata>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _bus.PublishAsync("dest", "payload");

        captured!.RoutingKey.Should().BeNull();
        captured!.Headers.Should().BeEmpty();
        captured!.CorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task PublishAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        await _bus.DisposeAsync();

        var act = () => _bus.PublishAsync("dest", "payload");

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Constructor_NullTransport_ThrowsArgumentNullException()
    {
        var act = () => new MessageBus((IMessageBusTransport)null!, _serializer);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullSerializer_ThrowsArgumentNullException()
    {
        var act = () => new MessageBus(_transport, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _bus.DisposeAsync();
    }

    public record TestPayload
    {
        public string Value { get; init; } = string.Empty;
    }
}
