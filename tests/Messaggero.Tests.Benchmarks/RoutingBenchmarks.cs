using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Messaggero.Routing;

namespace Messaggero.Tests.Benchmarks;

/// <summary>
/// T062: Routing resolution benchmark — 1,000-rule table, target ≤0.5ms p99 (SC-004).
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class RoutingBenchmarks
{
    private RoutingTable _table = null!;
    private string[] _messageTypes = null!;

    [Params(1000)]
    public int RuleCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rules = new List<RoutingRule>(RuleCount);
        _messageTypes = new string[RuleCount];

        for (var i = 0; i < RuleCount; i++)
        {
            var type = $"MessageType_{i}";
            _messageTypes[i] = type;
            rules.Add(new RoutingRule
            {
                MessageType = type,
                Transports = ["transport-a"],
                Destination = new Model.Destination { Name = type.ToLowerInvariant() }
            });
        }

        _table = new RoutingTable(rules);
    }

    [Benchmark]
    public RoutingRule ResolveFirst() => _table.Resolve(_messageTypes[0]);

    [Benchmark]
    public RoutingRule ResolveMiddle() => _table.Resolve(_messageTypes[RuleCount / 2]);

    [Benchmark]
    public RoutingRule ResolveLast() => _table.Resolve(_messageTypes[RuleCount - 1]);

    [Benchmark]
    public int ResolveAll()
    {
        var count = 0;
        for (var i = 0; i < _messageTypes.Length; i++)
        {
            _ = _table.Resolve(_messageTypes[i]);
            count++;
        }
        return count;
    }
}
