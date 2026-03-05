using Messaggero.Abstractions;

namespace Messaggero.Resilience;

/// <summary>
/// Implements exponential backoff reconnection logic with configurable parameters.
/// Emits lifecycle events during reconnection attempts and on exhaustion.
/// </summary>
public sealed class ExponentialBackoffReconnector
{
    private readonly ReconnectionOptions _options;
    private readonly string _transportName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialBackoffReconnector"/> class.
    /// </summary>
    /// <param name="options">The reconnection options defining backoff behavior.</param>
    /// <param name="transportName">The name of the transport being reconnected.</param>
    public ExponentialBackoffReconnector(ReconnectionOptions options, string transportName = "Unknown")
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transportName = transportName;
        _options.Validate();
    }

    /// <summary>
    /// Event raised when a lifecycle event occurs during reconnection.
    /// </summary>
    public event Action<LifecycleEvent>? OnEvent;

    /// <summary>
    /// Attempts to execute the connect action with exponential backoff.
    /// </summary>
    /// <param name="connectAction">The async action to attempt.</param>
    /// <param name="cancellationToken">A token to cancel the reconnection loop.</param>
    /// <exception cref="InvalidOperationException">Thrown when max attempts are exhausted.</exception>
    public async Task ReconnectAsync(Func<Task> connectAction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectAction);

        var attempt = 0;
        var delay = _options.InitialDelay;

        while (true)
        {
            try
            {
                attempt++;
                await connectAction().ConfigureAwait(false);
                return; // Success
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_options.MaxAttempts > 0 && attempt >= _options.MaxAttempts)
                {
                    EmitEvent(LifecycleEventType.TransportFailed, error: ex,
                        metadata: new Dictionary<string, object>
                        {
                            ["attempts"] = attempt,
                            ["lastError"] = ex.Message
                        });
                    throw;
                }

                EmitEvent(LifecycleEventType.TransportReconnecting,
                    metadata: new Dictionary<string, object>
                    {
                        ["attempt"] = attempt,
                        ["nextDelayMs"] = delay.TotalMilliseconds
                    });

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                // Calculate next delay with exponential backoff, capped at MaxDelay
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * _options.Multiplier,
                             _options.MaxDelay.TotalMilliseconds));
            }
        }
    }

    private void EmitEvent(
        LifecycleEventType type,
        Exception? error = null,
        Dictionary<string, object>? metadata = null)
    {
        var evt = new LifecycleEvent
        {
            EventType = type,
            TransportName = _transportName,
            Error = error,
            Metadata = metadata
        };

        OnEvent?.Invoke(evt);
    }
}
