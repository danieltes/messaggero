using System.Diagnostics;

namespace Messaggero.Observability;

/// <summary>
/// Distributed tracing for messaging operations using System.Diagnostics.
/// Creates Activity spans for publish and consume operations.
/// </summary>
public static class MessagingActivitySource
{
    /// <summary>The shared ActivitySource for all Messaggero tracing.</summary>
    public static readonly ActivitySource Source = new("Messaggero", "1.0.0");

    /// <summary>Starts a producer activity span for a publish operation.</summary>
    public static Activity? StartPublish(string messageType, string transportName, string? destination = null)
    {
        var activity = Source.StartActivity("publish", ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag("messaging.system", "messaggero");
            activity.SetTag("messaging.operation", "publish");
            activity.SetTag("messaging.message_type", messageType);
            activity.SetTag("messaging.destination", destination ?? messageType.ToLowerInvariant());
            activity.SetTag("messaging.transport", transportName);
        }
        return activity;
    }

    /// <summary>Starts a consumer activity span for a consume/handle operation.</summary>
    public static Activity? StartConsume(string messageType, string transportName, string messageId)
    {
        var activity = Source.StartActivity("consume", ActivityKind.Consumer);
        if (activity is not null)
        {
            activity.SetTag("messaging.system", "messaggero");
            activity.SetTag("messaging.operation", "consume");
            activity.SetTag("messaging.message_type", messageType);
            activity.SetTag("messaging.transport", transportName);
            activity.SetTag("messaging.message_id", messageId);
        }
        return activity;
    }
}
