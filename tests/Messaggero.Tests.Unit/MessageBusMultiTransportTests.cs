using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Routing;
using NSubstitute;

namespace Messaggero.Tests.Unit;

public class MessageBusMultiTransportTests : IAsyncDisposable
{
    private readonly IMessageSerializer _serializer = Substitute.For<IMessageSerializer>();

    public MessageBusMultiTransportTests()
    {
        _serializer.ContentType.Returns("application/json");
    }

    private static IMessageBusTransport CreateTransport(string name)
    {
        var transport = Substitute.For<IMessageBusTransport>();
        transport.Name.Returns(name);
        transport.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthCheckResult
        {
            IsHealthy = true,
            TransportName = name,
            Description = "OK"
        });
        return transport;
    }

    private MessageBus CreateBus(ITransportRouter router) =>
        new(router, _serializer);

    #region T010 — Builder multi-transport registration

    [Fact]
    public void AddTransport_RegistersByTransportName()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("Kafka");

        builder.AddTransport(transport);

        builder.TransportRegistry.Should().ContainKey("Kafka");
    }

    [Fact]
    public void AddTransport_WithExplicitName_RegistersByGivenName()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("Kafka");

        builder.AddTransport("MyKafka", transport);

        builder.TransportRegistry.Should().ContainKey("MyKafka");
    }

    [Fact]
    public void AddTransport_DuplicateName_Throws()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("Kafka"));

        var act = () => builder.AddTransport(CreateTransport("Kafka"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Kafka'*already registered*");
    }

    [Fact]
    public void UseDefaultTransport_SetsDefaultName()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));

        builder.UseDefaultTransport("RabbitMQ");

        builder.DefaultTransportName.Should().Be("RabbitMQ");
    }

    [Fact]
    public void UseTransport_BackwardCompat_RegistersByNameAndSetsDefault()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("RabbitMQ");

        builder.UseTransport(transport);

        builder.TransportRegistry.Should().ContainKey("RabbitMQ");
        builder.DefaultTransportName.Should().Be("RabbitMQ");
    }

    [Fact]
    public void Validate_NoTransports_Throws()
    {
        var builder = new MessageBusBuilder();

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No transport configured*");
    }

    [Fact]
    public void Validate_WithSingleTransport_Succeeds()
    {
        var builder = new MessageBusBuilder();
        builder.UseTransport(CreateTransport("RabbitMQ"));

        var act = () => builder.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MultipleTransports_Succeeds()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.AddTransport(CreateTransport("Kafka"));
        builder.UseDefaultTransport("RabbitMQ");

        var act = () => builder.Validate();

        act.Should().NotThrow();
    }

    #endregion

    #region T017 — Publish multi-transport routing

    [Fact]
    public async Task PublishAsync_RoutesToCorrectTransportByDestination()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("RabbitMQ", "notifications.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");
        _serializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        var bus = CreateBus(router);
        await bus.PublishAsync("orders.created", "test");

        await kafka.Received(1).PublishAsync("orders.created",
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<MessageMetadata>(), Arg.Any<CancellationToken>());
        await rabbit.DidNotReceive().PublishAsync(Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<MessageMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_FallsBackToDefaultTransport()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");
        _serializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        var bus = CreateBus(router);
        await bus.PublishAsync("notifications.sent", "test");

        await rabbit.Received(1).PublishAsync("notifications.sent",
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<MessageMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_NoMatchNoDefault_Throws()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*")
        };
        var router = new TransportRouter(transports, rules, [], null);
        _serializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        var bus = CreateBus(router);

        var act = () => bus.PublishAsync("notifications.sent", "test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot route*notifications.sent*");
    }

    #endregion

    #region T020 — Subscribe multi-transport routing

    [Fact]
    public async Task SubscribeAsync_RoutesToCorrectTransport()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("RabbitMQ", "notifications.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");

        var handler = Substitute.For<IMessageHandler<string>>();
        kafka.SubscribeAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());

        var bus = CreateBus(router);
        await bus.SubscribeAsync<string>("orders.created", "group1", handler);

        await kafka.Received(1).SubscribeAsync("orders.created", "group1",
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>());
        await rabbit.DidNotReceive().SubscribeAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_IndependentSubscriptionsOnDifferentTransports()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("RabbitMQ", "notifications.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");

        var handler1 = Substitute.For<IMessageHandler<string>>();
        var handler2 = Substitute.For<IMessageHandler<string>>();
        kafka.SubscribeAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());
        rabbit.SubscribeAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<ITransportSubscription>());

        var bus = CreateBus(router);
        await bus.SubscribeAsync<string>("orders.created", "group1", handler1);
        await bus.SubscribeAsync<string>("notifications.sent", "group2", handler2);

        await kafka.Received(1).SubscribeAsync("orders.created", Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>());
        await rabbit.Received(1).SubscribeAsync("notifications.sent", Arg.Any<string>(),
            Arg.Any<Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task>>(),
            Arg.Any<SubscriptionOptions>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region T027 — Aggregate health check

    [Fact]
    public async Task CheckHealthAsync_AllHealthy_ReturnsHealthy()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var bus = CreateBus(router);
        var result = await bus.CheckHealthAsync();

        result.IsHealthy.Should().BeTrue();
        result.TransportEntries.Should().HaveCount(2);
        result.TransportEntries.Should().OnlyContain(e => e.IsHealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_MixedHealth_ReturnsDegraded()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        kafka.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthCheckResult
        {
            IsHealthy = false,
            TransportName = "Kafka",
            Description = "Connection lost"
        });
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var bus = CreateBus(router);
        var result = await bus.CheckHealthAsync();

        result.IsHealthy.Should().BeFalse();
        result.Description.Should().Contain("degraded");
        result.TransportEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckHealthAsync_AllUnhealthy_ReturnsUnhealthy()
    {
        var rabbit = CreateTransport("RabbitMQ");
        rabbit.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthCheckResult
        {
            IsHealthy = false,
            TransportName = "RabbitMQ",
            Description = "Down"
        });
        var kafka = CreateTransport("Kafka");
        kafka.CheckHealthAsync(Arg.Any<CancellationToken>()).Returns(new HealthCheckResult
        {
            IsHealthy = false,
            TransportName = "Kafka",
            Description = "Down"
        });
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var bus = CreateBus(router);
        var result = await bus.CheckHealthAsync();

        result.IsHealthy.Should().BeFalse();
        result.Description.Should().Contain("unhealthy");
    }

    [Fact]
    public async Task CheckHealthAsync_SingleTransport_BackwardCompatible()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit
        };
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var bus = CreateBus(router);
        var result = await bus.CheckHealthAsync();

        result.IsHealthy.Should().BeTrue();
        result.TransportName.Should().Be("RabbitMQ");
        result.TransportEntries.Should().HaveCount(1);
    }

    #endregion

    #region T028 — Lifecycle event forwarding

    [Fact]
    public async Task OnLifecycleEvent_ForwardsFromAllTransports()
    {
        var rabbit = CreateTransport("RabbitMQ");
        var kafka = CreateTransport("Kafka");
        var capturedListeners = new Dictionary<string, Action<LifecycleEvent>>();

        rabbit.OnLifecycleEvent(Arg.Do<Action<LifecycleEvent>>(l => capturedListeners["RabbitMQ"] = l))
            .Returns(Substitute.For<IDisposable>());
        kafka.OnLifecycleEvent(Arg.Do<Action<LifecycleEvent>>(l => capturedListeners["Kafka"] = l))
            .Returns(Substitute.For<IDisposable>());

        var transports = new Dictionary<string, IMessageBusTransport>
        {
            ["RabbitMQ"] = rabbit,
            ["Kafka"] = kafka
        };
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var bus = CreateBus(router);
        var received = new List<LifecycleEvent>();
        bus.OnLifecycleEvent(e => received.Add(e));

        // Simulate events from each transport
        if (capturedListeners.TryGetValue("RabbitMQ", out var rabbitListener))
        {
            rabbitListener(new LifecycleEvent
            {
                EventType = LifecycleEventType.TransportConnected,
                TransportName = "RabbitMQ"
            });
        }

        if (capturedListeners.TryGetValue("Kafka", out var kafkaListener))
        {
            kafkaListener(new LifecycleEvent
            {
                EventType = LifecycleEventType.TransportConnected,
                TransportName = "Kafka"
            });
        }

        received.Should().HaveCount(2);
        received.Should().Contain(e => e.TransportName == "RabbitMQ");
        received.Should().Contain(e => e.TransportName == "Kafka");

        await bus.DisposeAsync();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
