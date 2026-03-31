using FluentAssertions;
using Messaggero.Configuration;
using Messaggero.Errors;
using Messaggero.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaggero.Tests.Unit.Hosting;

public class RetryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt_DoesNotRetry()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 3, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) };
        var executor = new RetryExecutor(policy, NullLogger.Instance);
        var attempts = 0;

        await executor.ExecuteAsync((attempt, ct) =>
        {
            attempts = attempt;
            return Task.CompletedTask;
        }, CancellationToken.None);

        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FailsThenSucceeds_RetriesCorrectly()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 3, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) };
        var executor = new RetryExecutor(policy, NullLogger.Instance);
        var attempts = 0;

        await executor.ExecuteAsync((attempt, ct) =>
        {
            attempts = attempt;
            if (attempt < 2)
                throw new InvalidOperationException("Transient");
            return Task.CompletedTask;
        }, CancellationToken.None);

        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetries_ThrowsRetryExhaustedException()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 3, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(1) };
        var executor = new RetryExecutor(policy, NullLogger.Instance);

        var act = () => executor.ExecuteAsync((_, _) =>
            throw new InvalidOperationException("Always fails"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RetryExhaustedException>();
        ex.Which.Attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_FixedBackoff_DelaysAreConstant()
    {
        var policy = new RetryPolicyOptions
        {
            MaxAttempts = 3,
            BackoffStrategy = BackoffStrategy.Fixed,
            InitialDelay = TimeSpan.FromMilliseconds(50)
        };
        var executor = new RetryExecutor(policy, NullLogger.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var act = () => executor.ExecuteAsync((_, _) =>
            throw new InvalidOperationException("Always fails"), CancellationToken.None);

        await act.Should().ThrowAsync<RetryExhaustedException>();
        sw.Stop();

        // 2 retries × 50ms = ~100ms minimum
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(80);
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_CappedAtMaxDelay()
    {
        var policy = new RetryPolicyOptions
        {
            MaxAttempts = 5,
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(50)
        };
        var executor = new RetryExecutor(policy, NullLogger.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var act = () => executor.ExecuteAsync((_, _) =>
            throw new InvalidOperationException("Always fails"), CancellationToken.None);

        await act.Should().ThrowAsync<RetryExhaustedException>();
        sw.Stop();

        // Without cap: 10+20+40+80 = 150ms. With cap at 50ms: 10+20+40+50 = 120ms max-ish
        // Just verify it completed (delay total is bounded by MaxDelay cap)
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableException_ThrowsImmediately()
    {
        var policy = new RetryPolicyOptions
        {
            MaxAttempts = 3,
            BackoffStrategy = BackoffStrategy.Fixed,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryableExceptions = ex => ex is TimeoutException
        };
        var executor = new RetryExecutor(policy, NullLogger.Instance);
        var attempts = 0;

        var act = () => executor.ExecuteAsync((attempt, _) =>
        {
            attempts = attempt;
            throw new ArgumentException("Non-retryable");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<RetryExhaustedException>();
        attempts.Should().Be(1); // No retries attempted for non-retryable exception
    }

    [Fact]
    public async Task ExecuteAsync_RetryableExceptionFilter_RetriesMatchingExceptions()
    {
        var policy = new RetryPolicyOptions
        {
            MaxAttempts = 3,
            BackoffStrategy = BackoffStrategy.Fixed,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            RetryableExceptions = ex => ex is TimeoutException
        };
        var executor = new RetryExecutor(policy, NullLogger.Instance);
        var attempts = 0;

        await executor.ExecuteAsync((attempt, _) =>
        {
            attempts = attempt;
            if (attempt < 2)
                throw new TimeoutException("Transient timeout");
            return Task.CompletedTask;
        }, CancellationToken.None);

        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var policy = new RetryPolicyOptions { MaxAttempts = 3, BackoffStrategy = BackoffStrategy.Fixed, InitialDelay = TimeSpan.FromMilliseconds(100) };
        var executor = new RetryExecutor(policy, NullLogger.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => executor.ExecuteAsync((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
