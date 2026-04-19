using Assertivo;
using Messaggero.Errors;
using Messaggero.Routing;
using Xunit;

namespace Messaggero.Tests.Unit.Routing;

public class RoutingTableTests
{
    [Fact]
    public void Resolve_ExistingMessageType_ReturnsRule()
    {
        var rule = new RoutingRule
        {
            MessageType = "OrderPlaced",
            Transports = ["kafka"]
        };
        var table = new RoutingTable([rule]);

        var result = table.Resolve("OrderPlaced");

        result.Should().BeSameAs(rule);
    }

    [Fact]
    public void Resolve_NonExistentMessageType_ThrowsNoRouteFoundException()
    {
        var table = new RoutingTable([]);

        var act = () => table.Resolve("UnknownMessage");

        var ex = Assert.Throws<NoRouteFoundException>(act);
        ex.MessageType.Should().Be("UnknownMessage");
    }

    [Fact]
    public void Resolve_MultipleRules_ReturnsCorrectRule()
    {
        var kafkaRule = new RoutingRule
        {
            MessageType = "OrderPlaced",
            Transports = ["kafka"]
        };
        var rabbitRule = new RoutingRule
        {
            MessageType = "EmailRequested",
            Transports = ["rabbitmq"]
        };
        var table = new RoutingTable([kafkaRule, rabbitRule]);

        table.Resolve("OrderPlaced").Should().BeSameAs(kafkaRule);
        table.Resolve("EmailRequested").Should().BeSameAs(rabbitRule);
    }

    [Fact]
    public void Resolve_IsCaseSensitive()
    {
        var rule = new RoutingRule
        {
            MessageType = "OrderPlaced",
            Transports = ["kafka"]
        };
        var table = new RoutingTable([rule]);

        var act = () => table.Resolve("orderplaced");

        Assert.Throws<NoRouteFoundException>(act);
    }

    [Fact]
    public void Constructor_DuplicateMessageType_LastWins()
    {
        var rule1 = new RoutingRule { MessageType = "OrderPlaced", Transports = ["kafka"] };
        var rule2 = new RoutingRule { MessageType = "OrderPlaced", Transports = ["rabbitmq"] };
        var table = new RoutingTable([rule1, rule2]);

        var result = table.Resolve("OrderPlaced");

        Assert.Contains("rabbitmq", result.Transports);
    }

    [Fact]
    public void Rules_ReturnsAllRegisteredRules()
    {
        var rules = new[]
        {
            new RoutingRule { MessageType = "A", Transports = ["t1"] },
            new RoutingRule { MessageType = "B", Transports = ["t2"] },
            new RoutingRule { MessageType = "C", Transports = ["t3"] }
        };
        var table = new RoutingTable(rules);

        Assert.Equal(3, table.Rules.Count);
        Assert.Equivalent(new[] { "A", "B", "C" }, table.Rules.Keys);
    }
}
