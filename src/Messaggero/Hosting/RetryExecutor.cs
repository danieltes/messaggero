using Messaggero.Configuration;
using Messaggero.Errors;
using Messaggero.Observability;
using Microsoft.Extensions.Logging;

namespace Messaggero.Hosting;

/// <summary>
/// Executes handler invocations with configurable retry and dead-letter routing.
/// </summary>
public sealed class RetryExecutor
{
    private readonly RetryPolicyOptions _policy;
    private readonly ILogger _logger;

    public RetryExecutor(RetryPolicyOptions policy, ILogger logger)
    {
        _policy = policy;
        _logger = logger;
    }

    /// <summary>
    /// Executes the given action with retry according to the configured policy.
    /// </summary>
    public async Task ExecuteAsync(Func<int, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _policy.MaxAttempts; attempt++)
        {
            try
            {
                await action(attempt, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (_policy.RetryableExceptions is not null && !_policy.RetryableExceptions(ex))
                {
                    throw new RetryExhaustedException(attempt,
                        $"Non-retryable exception on attempt {attempt}.", ex);
                }

                if (attempt < _policy.MaxAttempts)
                {
                    MessagingMetrics.MessagesRetried.Add(1);

                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(ex,
                        "Retry attempt {Attempt}/{MaxAttempts}, next retry in {DelayMs}ms",
                        attempt, _policy.MaxAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        MessagingMetrics.MessagesDeadLettered.Add(1);
        throw new RetryExhaustedException(_policy.MaxAttempts,
            $"All {_policy.MaxAttempts} retry attempts exhausted.", lastException!);
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        var delay = _policy.BackoffStrategy switch
        {
            BackoffStrategy.Fixed => _policy.InitialDelay,
            BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(
                _policy.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
            _ => _policy.InitialDelay
        };

        return delay > _policy.MaxDelay ? _policy.MaxDelay : delay;
    }
}
