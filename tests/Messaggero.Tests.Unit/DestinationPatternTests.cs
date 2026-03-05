using FluentAssertions;
using Messaggero.Routing;

namespace Messaggero.Tests.Unit;

public class DestinationPatternTests
{
    [Theory]
    [InlineData("orders.created", "orders.created", true)]
    [InlineData("orders.created", "orders.updated", false)]
    [InlineData("orders.created", "orders", false)]
    [InlineData("orders.created", "orders.created.extra", false)]
    public void ExactMatch_MatchesOnlyIdenticalStrings(string pattern, string destination, bool expected)
    {
        var dp = new DestinationPattern(pattern);

        dp.IsMatch(destination).Should().Be(expected);
        dp.Specificity.Should().Be(PatternSpecificity.Exact);
    }

    [Theory]
    [InlineData("orders.*", "orders.created", true)]
    [InlineData("orders.*", "orders.updated", true)]
    [InlineData("orders.*", "orders", false)]
    [InlineData("orders.*", "orders.created.detail", false)]
    [InlineData("*.created", "orders.created", true)]
    [InlineData("*.created", "events.created", true)]
    [InlineData("*.created", "created", false)]
    [InlineData("*.*", "orders.created", true)]
    [InlineData("*.*", "orders", false)]
    public void SingleWildcard_MatchesExactlyOneSegment(string pattern, string destination, bool expected)
    {
        var dp = new DestinationPattern(pattern);

        dp.IsMatch(destination).Should().Be(expected);
        dp.Specificity.Should().Be(PatternSpecificity.SingleWildcard);
    }

    [Theory]
    [InlineData("orders.**", "orders.created", true)]
    [InlineData("orders.**", "orders.created.detail", true)]
    [InlineData("orders.**", "orders.created.detail.extra", true)]
    [InlineData("orders.**", "orders", false)]
    [InlineData("**", "orders", true)]
    [InlineData("**", "orders.created", true)]
    [InlineData("**", "a.b.c.d", true)]
    [InlineData("**.created", "orders.created", true)]
    [InlineData("**.created", "a.b.created", true)]
    [InlineData("**.created", "created", false)]
    public void MultiWildcard_MatchesOneOrMoreSegments(string pattern, string destination, bool expected)
    {
        var dp = new DestinationPattern(pattern);

        dp.IsMatch(destination).Should().Be(expected);
        dp.Specificity.Should().Be(PatternSpecificity.MultiWildcard);
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrWhitespace()
    {
        var act1 = () => new DestinationPattern(null!);
        var act2 = () => new DestinationPattern("");
        var act3 = () => new DestinationPattern("   ");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RawPattern_ReturnsOriginalString()
    {
        var dp = new DestinationPattern("orders.*");
        dp.RawPattern.Should().Be("orders.*");
    }

    [Theory]
    [InlineData("a", 0)]          // Exact
    [InlineData("a.b.c", 0)]      // Exact
    [InlineData("a.*", 1)]        // SingleWildcard
    [InlineData("*.b.*", 1)]      // SingleWildcard
    [InlineData("a.**", 2)]       // MultiWildcard
    [InlineData("**", 2)]         // MultiWildcard
    public void Specificity_IsCorrectlyDetermined(string pattern, int expectedSpecificity)
    {
        var dp = new DestinationPattern(pattern);
        dp.Specificity.Should().Be((PatternSpecificity)expectedSpecificity);
    }

    [Theory]
    [InlineData("a.*.c", "a.b.c", true)]
    [InlineData("a.*.c", "a.x.c", true)]
    [InlineData("a.*.c", "a.b.d", false)]
    [InlineData("a.**.c", "a.b.c", true)]
    [InlineData("a.**.c", "a.b.d.c", true)]
    [InlineData("a.**.c", "a.c", false)]
    public void MixedPatterns_MatchCorrectly(string pattern, string destination, bool expected)
    {
        var dp = new DestinationPattern(pattern);
        dp.IsMatch(destination).Should().Be(expected);
    }
}
