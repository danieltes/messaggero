using FluentAssertions;
using Messaggero.Serialization;

namespace Messaggero.Tests.Unit;

public class JsonMessageSerializerTests
{
    private readonly JsonMessageSerializer _serializer = new();

    [Fact]
    public void ContentType_ShouldBeApplicationJson()
    {
        _serializer.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void Serialize_Deserialize_String_ShouldRoundTrip()
    {
        var original = "hello world";
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<string>(bytes);

        result.Should().Be(original);
    }

    [Fact]
    public void Serialize_Deserialize_Integer_ShouldRoundTrip()
    {
        var original = 42;
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<int>(bytes);

        result.Should().Be(original);
    }

    [Fact]
    public void Serialize_Deserialize_ComplexType_ShouldRoundTrip()
    {
        var original = new TestPayload
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Name = "Test Item",
            Amount = 123.45m,
            Tags = ["tag1", "tag2"]
        };

        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<TestPayload>(bytes);

        result.Id.Should().Be(original.Id);
        result.Name.Should().Be(original.Name);
        result.Amount.Should().Be(original.Amount);
        result.Tags.Should().BeEquivalentTo(original.Tags);
    }

    [Fact]
    public void Serialize_Deserialize_EmptyObject_ShouldRoundTrip()
    {
        var original = new EmptyPayload();
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<EmptyPayload>(bytes);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Serialize_Deserialize_WithNullableFields_ShouldPreserveNulls()
    {
        var original = new NullablePayload { Name = null, Value = null };
        var bytes = _serializer.Serialize(original);
        var result = _serializer.Deserialize<NullablePayload>(bytes);

        result.Name.Should().BeNull();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Serialize_Null_ShouldProduceValidBytes()
    {
        var bytes = _serializer.Serialize<string?>(null);

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Deserialize_NullJson_ShouldThrowJsonException()
    {
        var nullBytes = "null"u8.ToArray();

        var act = () => _serializer.Deserialize<TestPayload>(nullBytes);

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void Serialize_ProducesNonEmptyBytes()
    {
        var bytes = _serializer.Serialize(new { key = "value" });

        bytes.Should().NotBeEmpty();
    }

    public record TestPayload
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public List<string> Tags { get; init; } = [];
    }

    public record EmptyPayload;

    public record NullablePayload
    {
        public string? Name { get; init; }
        public int? Value { get; init; }
    }
}
