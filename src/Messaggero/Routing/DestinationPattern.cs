namespace Messaggero.Routing;

/// <summary>
/// Specificity levels for destination patterns, ordered from most to least specific.
/// </summary>
internal enum PatternSpecificity
{
    /// <summary>No wildcards — exact string match.</summary>
    Exact = 0,

    /// <summary>Contains single-segment wildcard (*) but not multi-segment (**).</summary>
    SingleWildcard = 1,

    /// <summary>Contains multi-segment wildcard (**).</summary>
    MultiWildcard = 2
}

/// <summary>
/// A compiled glob pattern for allocation-free destination matching.
/// Segments are separated by '.'. '*' matches one segment, '**' matches one or more segments.
/// </summary>
internal sealed class DestinationPattern
{
    private readonly Func<string, bool> _matcher;

    /// <summary>
    /// The original pattern string (e.g., "orders.*").
    /// </summary>
    public string RawPattern { get; }

    /// <summary>
    /// The specificity level of this pattern.
    /// </summary>
    public PatternSpecificity Specificity { get; }

    public DestinationPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        RawPattern = pattern;
        Specificity = DetermineSpecificity(pattern);
        _matcher = CompileMatcher(pattern, Specificity);
    }

    /// <summary>
    /// Tests whether the given destination matches this pattern.
    /// </summary>
    public bool IsMatch(string destination) => _matcher(destination);

    private static PatternSpecificity DetermineSpecificity(string pattern)
    {
        if (pattern.Contains("**"))
            return PatternSpecificity.MultiWildcard;
        if (pattern.Contains('*'))
            return PatternSpecificity.SingleWildcard;
        return PatternSpecificity.Exact;
    }

    private static Func<string, bool> CompileMatcher(string pattern, PatternSpecificity specificity)
    {
        if (specificity == PatternSpecificity.Exact)
        {
            // Exact match — simple string comparison
            return destination => string.Equals(destination, pattern, StringComparison.Ordinal);
        }

        var patternSegments = pattern.Split('.');
        return destination => MatchSegments(patternSegments, destination.Split('.'), 0, 0);
    }

    private static bool MatchSegments(string[] pattern, string[] destination, int pi, int di)
    {
        while (pi < pattern.Length && di < destination.Length)
        {
            if (pattern[pi] == "**")
            {
                // '**' matches one or more segments
                // Try matching the rest of the pattern starting from each remaining destination segment
                for (var i = di + 1; i <= destination.Length; i++)
                {
                    if (pi + 1 >= pattern.Length)
                    {
                        // '**' is the last pattern segment — matches all remaining
                        return true;
                    }

                    if (MatchSegments(pattern, destination, pi + 1, i))
                        return true;
                }

                return false;
            }

            if (pattern[pi] != "*" && !string.Equals(pattern[pi], destination[di], StringComparison.Ordinal))
            {
                return false;
            }

            pi++;
            di++;
        }

        // Both must be exhausted
        return pi == pattern.Length && di == destination.Length;
    }
}
