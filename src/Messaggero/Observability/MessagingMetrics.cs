using System.Diagnostics.Metrics;

namespace Messaggero.Observability;

/// <summary>
/// Metrics for messaging operations using System.Diagnostics.Metrics.
/// </summary>
public sealed class MessagingMetrics
{
    /// <summary>The shared Meter for all Messaggero metrics.</summary>
    public static readonly Meter Meter = new("Messaggero", "1.0.0");

    /// <summary>Counter: total messages published.</summary>
    public static readonly Counter<long> MessagesPublished =
        Meter.CreateCounter<long>("messaggero.messages.published", "messages", "Total messages published");

    /// <summary>Counter: total messages consumed.</summary>
    public static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>("messaggero.messages.consumed", "messages", "Total messages consumed");

    /// <summary>Counter: total retry attempts.</summary>
    public static readonly Counter<long> MessagesRetried =
        Meter.CreateCounter<long>("messaggero.messages.retried", "messages", "Total retry attempts");

    /// <summary>Counter: total messages sent to dead-letter.</summary>
    public static readonly Counter<long> MessagesDeadLettered =
        Meter.CreateCounter<long>("messaggero.messages.dead_lettered", "messages", "Total messages sent to dead-letter");

    /// <summary>Histogram: publish operation duration in milliseconds.</summary>
    public static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>("messaggero.publish.duration", "ms", "Publish operation duration in milliseconds");

    /// <summary>Histogram: consume/handle operation duration in milliseconds.</summary>
    public static readonly Histogram<double> ConsumeDuration =
        Meter.CreateHistogram<double>("messaggero.consume.duration", "ms", "Consume/handle operation duration in milliseconds");
}
