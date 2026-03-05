using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Routing;
using NSubstitute;

namespace Messaggero.Tests.Unit;

public class TransportRouterTests
{
    private static IMessageBusTransport CreateTransport(string name)
    {
        var transport = Substitute.For<IMessageBusTransport>();
        transport.Name.Returns(name);
        return transport;
    }

    private static Dictionary<string, IMessageBusTransport> CreateTransports(params string[] names)
    {
        var dict = new Dictionary<string, IMessageBusTransport>();
        foreach (var name in names)
        {
            dict[name] = CreateTransport(name);
        }
        return dict;
    }

    [Fact]
    public void ResolveTransport_SingleTransportNoRules_ReturnsImplicitDefault()
    {
        var transports = CreateTransports("RabbitMQ");
        var router = new TransportRouter(transports, [], [], null);

        var result = router.ResolveTransport("any.destination");

        result.Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_ExplicitDefault_ReturnsDefault()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var router = new TransportRouter(transports, [], [], "RabbitMQ");

        var result = router.ResolveTransport("unrouted.destination");

        result.Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_DestinationRule_MatchesCorrectTransport()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("RabbitMQ", "notifications.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");

        router.ResolveTransport("orders.created").Name.Should().Be("Kafka");
        router.ResolveTransport("notifications.sent").Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_MostSpecificDestinationRuleWins()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("RabbitMQ", "orders.**"),
            RoutingRule.ForDestination("Kafka", "orders.created")
        };
        var router = new TransportRouter(transports, rules, [], null);

        // Exact match should win over multi-wildcard
        router.ResolveTransport("orders.created").Name.Should().Be("Kafka");
        // Multi-wildcard catches everything else
        router.ResolveTransport("orders.updated").Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_NoMatchNoDefault_Throws()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*")
        };
        var router = new TransportRouter(transports, rules, [], null);

        var act = () => router.ResolveTransport("notifications.sent");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot route*notifications.sent*");
    }

    [Fact]
    public void Constructor_ConflictingDestinationRules_Throws()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("RabbitMQ", "orders.*")
        };

        var act = () => new TransportRouter(transports, rules, [], null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting routing rules*orders.*");
    }

    [Fact]
    public void Constructor_UnregisteredTransportInRule_Throws()
    {
        var transports = CreateTransports("RabbitMQ");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("NonExistent", "orders.*")
        };

        var act = () => new TransportRouter(transports, rules, [], null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Routing rule references transport 'NonExistent'*not registered*");
    }

    [Fact]
    public void Constructor_UnregisteredDefaultTransport_Throws()
    {
        var transports = CreateTransports("RabbitMQ");

        var act = () => new TransportRouter(transports, [], [], "NonExistent");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Default transport 'NonExistent' is not registered*");
    }

    [Fact]
    public void Constructor_NoTransports_Throws()
    {
        var transports = new Dictionary<string, IMessageBusTransport>();

        var act = () => new TransportRouter(transports, [], [], null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*At least one transport*");
    }

    [Fact]
    public void Constructor_DuplicateDestinationPatternSameTransport_Succeeds()
    {
        var transports = CreateTransports("Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*"),
            RoutingRule.ForDestination("Kafka", "orders.*")
        };

        // Same pattern, same transport — no conflict
        var act = () => new TransportRouter(transports, rules, [], null);
        act.Should().NotThrow();
    }

    [Fact]
    public void Transports_ReturnsAllRegistered()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var router = new TransportRouter(transports, [], [], null);

        router.Transports.Should().HaveCount(2);
        router.Transports.Keys.Should().Contain("RabbitMQ").And.Contain("Kafka");
    }

    [Fact]
    public void ResolveTransport_DefaultFallbackAfterNoDestinationMatch()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var rules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("Kafka", "orders.*")
        };
        var router = new TransportRouter(transports, rules, [], "RabbitMQ");

        // No match → falls through to default
        router.ResolveTransport("events.something").Name.Should().Be("RabbitMQ");
    }

    #region T023 — Type-based resolution

    [Fact]
    public void ResolveTransport_ExactTypeMatch()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(OrderEvent))
        };
        var router = new TransportRouter(transports, [], typeRules, "RabbitMQ");

        router.ResolveTransport("any.dest", typeof(OrderEvent)).Name.Should().Be("Kafka");
    }

    [Fact]
    public void ResolveTransport_BaseClassHierarchyWalk()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(BaseEvent))
        };
        var router = new TransportRouter(transports, [], typeRules, "RabbitMQ");

        // DerivedEvent inherits from BaseEvent — should walk hierarchy
        router.ResolveTransport("any.dest", typeof(DerivedEvent)).Name.Should().Be("Kafka");
    }

    [Fact]
    public void ResolveTransport_MostDerivedFirstOrdering()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("RabbitMQ", typeof(BaseEvent)),
            RoutingRule.ForType("Kafka", typeof(OrderEvent))
        };
        var router = new TransportRouter(transports, [], typeRules, null);

        // OrderEvent is more derived — should match first
        router.ResolveTransport("any.dest", typeof(OrderEvent)).Name.Should().Be("Kafka");
        // BaseEvent matches directly
        router.ResolveTransport("any.dest", typeof(BaseEvent)).Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_DestinationRuleTakesPrecedenceOverTypeRule()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var destRules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("RabbitMQ", "orders.*")
        };
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(OrderEvent))
        };
        var router = new TransportRouter(transports, destRules, typeRules, null);

        // Destination should win over type
        router.ResolveTransport("orders.created", typeof(OrderEvent)).Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void ResolveTransport_TypeRuleFallbackWhenNoDestinationMatch()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var destRules = new List<RoutingRule>
        {
            RoutingRule.ForDestination("RabbitMQ", "orders.*")
        };
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(OrderEvent))
        };
        var router = new TransportRouter(transports, destRules, typeRules, "RabbitMQ");

        // No destination match → type rule kicks in
        router.ResolveTransport("events.something", typeof(OrderEvent)).Name.Should().Be("Kafka");
    }

    [Fact]
    public void ResolveTransport_CachedTypeResolution()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(OrderEvent))
        };
        var router = new TransportRouter(transports, [], typeRules, "RabbitMQ");

        // First call populates cache, second uses it
        router.ResolveTransport("any.dest", typeof(OrderEvent)).Name.Should().Be("Kafka");
        router.ResolveTransport("any.dest", typeof(OrderEvent)).Name.Should().Be("Kafka");
    }

    [Fact]
    public void Constructor_ConflictingTypeRules_Throws()
    {
        var transports = CreateTransports("RabbitMQ", "Kafka");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("Kafka", typeof(OrderEvent)),
            RoutingRule.ForType("RabbitMQ", typeof(OrderEvent))
        };

        var act = () => new TransportRouter(transports, [], typeRules, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting routing rules*OrderEvent*");
    }

    [Fact]
    public void Constructor_UnregisteredTransportInTypeRule_Throws()
    {
        var transports = CreateTransports("RabbitMQ");
        var typeRules = new List<RoutingRule>
        {
            RoutingRule.ForType("NonExistent", typeof(OrderEvent))
        };

        var act = () => new TransportRouter(transports, [], typeRules, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Routing rule references transport 'NonExistent'*not registered*");
    }

    // Test types for type-based routing — intentionally not sealed for hierarchy walk testing
#pragma warning disable CA1852
    private class BaseEvent;
    private class OrderEvent : BaseEvent;
    private class DerivedEvent : BaseEvent;
#pragma warning restore CA1852

    #endregion
}
