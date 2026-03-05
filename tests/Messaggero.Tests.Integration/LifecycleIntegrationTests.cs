using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using Messaggero.Tests.Integration.Fixtures;
using Messaggero.Transport.RabbitMQ;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero.Tests.Integration;

[Collection("RabbitMQ")]
public class LifecycleIntegrationTests : IAsyncDisposable
{
    private readonly RabbitMqFixture _fixture;
    private RabbitMqTransport? _transport;
    private MessageBus? _bus;

    public LifecycleIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OnLifecycleEvent_EmitsTransportConnected()
    {
        var transport = CreateTransport();
        var events = new List<LifecycleEvent>();
        transport.OnLifecycleEvent(e => events.Add(e));

        await transport.ConnectAsync();

        events.Should().Contain(e => e.EventType == LifecycleEventType.TransportConnected);
    }

    [Fact]
    public async Task OnLifecycleEvent_EmitsTransportDisconnected()
    {
        var transport = CreateTransport();
        var events = new List<LifecycleEvent>();
        await transport.ConnectAsync();
        transport.OnLifecycleEvent(e => events.Add(e));

        await transport.DisconnectAsync();

        events.Should().Contain(e => e.EventType == LifecycleEventType.TransportDisconnected);
    }

    [Fact]
    public async Task MessageBus_PublishAsync_EmitsMessagePublished()
    {
        var transport = CreateTransport();
        await transport.ConnectAsync();
        var serializer = new JsonMessageSerializer();
        var bus = new MessageBus(transport, serializer);
        _bus = bus;

        var events = new List<LifecycleEvent>();
        bus.OnLifecycleEvent(e => events.Add(e));

        var destination = $"lifecycle.publish.{Guid.NewGuid():N}";
        await bus.PublishAsync(destination, "hello");

        events.Should().Contain(e =>
            e.EventType == LifecycleEventType.MessagePublished &&
            e.Destination == destination);
    }

    [Fact]
    public async Task HealthCheck_WhenConnected_ReportsHealthy()
    {
        var transport = CreateTransport();
        await transport.ConnectAsync();
        var serializer = new JsonMessageSerializer();
        var bus = new MessageBus(transport, serializer);
        _bus = bus;

        var health = await bus.CheckHealthAsync();

        health.IsHealthy.Should().BeTrue();
        health.TransportName.Should().Be("RabbitMQ");
    }

    [Fact]
    public async Task HealthCheck_WhenNotConnected_ReportsUnhealthy()
    {
        var transport = CreateTransport();
        // Don't connect
        var serializer = new JsonMessageSerializer();
        var bus = new MessageBus(transport, serializer);
        _bus = bus;

        var health = await bus.CheckHealthAsync();

        health.IsHealthy.Should().BeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_bus is not null)
            await _bus.DisposeAsync();
        else if (_transport is not null)
            await _transport.DisposeAsync();
    }

    private RabbitMqTransport CreateTransport()
    {
        var config = new RabbitMqConfiguration
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = "guest",
            Password = "guest"
        };

        _transport = new RabbitMqTransport(config, NullLogger<RabbitMqTransport>.Instance);
        return _transport;
    }
}
