using FluentAssertions;
using Messaggero.Abstractions;

namespace Messaggero.Tests.Unit;

public class MessageEnvelopeTests
{
    [Fact]
    public void Envelope_WithAllRequiredFields_ShouldCreateSuccessfully()
    {
        var envelope = new MessageEnvelope<string>
        {
            MessageId = "msg-001",
            Payload = "test-payload",
            Destination = "orders.created"
        };

        envelope.MessageId.Should().Be("msg-001");
        envelope.Payload.Should().Be("test-payload");
        envelope.Destination.Should().Be("orders.created");
        envelope.Headers.Should().BeEmpty();
        envelope.RoutingKey.Should().BeNull();
        envelope.ContentType.Should().Be("application/json");
        envelope.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void Envelope_WithOptionalFields_ShouldRetainValues()
    {
        var headers = new Dictionary<string, string> { ["key"] = "value" };
        var timestamp = DateTimeOffset.UtcNow;

        var envelope = new MessageEnvelope<int>
        {
            MessageId = "msg-002",
            Payload = 42,
            Destination = "metrics.count",
            Headers = headers,
            RoutingKey = "partition-key",
            Timestamp = timestamp,
            ContentType = "application/json",
            CorrelationId = "corr-123"
        };

        envelope.Headers.Should().ContainKey("key").WhoseValue.Should().Be("value");
        envelope.RoutingKey.Should().Be("partition-key");
        envelope.Timestamp.Should().Be(timestamp);
        envelope.CorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public void Envelope_DefaultTimestamp_ShouldBeDefault()
    {
        var envelope = new MessageEnvelope<string>
        {
            MessageId = "msg-003",
            Payload = "test",
            Destination = "test.dest"
        };

        envelope.Timestamp.Should().Be(default);
    }

    [Fact]
    public void Envelope_DefaultContentType_ShouldBeJson()
    {
        var envelope = new MessageEnvelope<string>
        {
            MessageId = "msg-004",
            Payload = "test",
            Destination = "test.dest"
        };

        envelope.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Envelope_IsImmutableRecord_ShouldSupportWithExpression()
    {
        var original = new MessageEnvelope<string>
        {
            MessageId = "msg-005",
            Payload = "original",
            Destination = "test.dest"
        };

        var modified = original with { Payload = "modified" };

        modified.Payload.Should().Be("modified");
        modified.MessageId.Should().Be("msg-005");
        original.Payload.Should().Be("original");
    }

    [Fact]
    public void Envelope_EqualityByValue_ShouldCompareAllFields()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var a = new MessageEnvelope<string>
        {
            MessageId = "msg-006",
            Payload = "test",
            Destination = "dest",
            Timestamp = timestamp
        };

        var b = new MessageEnvelope<string>
        {
            MessageId = "msg-006",
            Payload = "test",
            Destination = "dest",
            Timestamp = timestamp
        };

        a.Should().BeEquivalentTo(b);
    }

    [Fact]
    public void Envelope_WithComplexPayload_ShouldRetainPayload()
    {
        var payload = new TestOrder { OrderId = Guid.NewGuid(), Total = 99.95m };

        var envelope = new MessageEnvelope<TestOrder>
        {
            MessageId = "msg-007",
            Payload = payload,
            Destination = "orders.created"
        };

        envelope.Payload.OrderId.Should().Be(payload.OrderId);
        envelope.Payload.Total.Should().Be(99.95m);
    }

    public record TestOrder
    {
        public Guid OrderId { get; init; }
        public decimal Total { get; init; }
    }
}
