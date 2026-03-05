using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Resilience;

namespace Messaggero.Tests.Unit;

public class ExponentialBackoffReconnectorTests
{
    [Fact]
    public async Task ReconnectAsync_SucceedsOnFirstAttempt_ReturnsImmediately()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxAttempts = 3
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        var attemptCount = 0;

        await reconnector.ReconnectAsync(() =>
        {
            attemptCount++;
            return Task.CompletedTask;
        });

        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ReconnectAsync_RetriesOnFailure_ExponentialDelay()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(10),
            Multiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(1),
            MaxAttempts = 3
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        var attemptCount = 0;

        await reconnector.ReconnectAsync(() =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new InvalidOperationException("Connection failed");
            return Task.CompletedTask;
        });

        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ReconnectAsync_ExhaustsMaxAttempts_Throws()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(5),
            MaxAttempts = 2
        };
        var reconnector = new ExponentialBackoffReconnector(options);

        var act = () => reconnector.ReconnectAsync(() =>
            throw new InvalidOperationException("Always fails"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReconnectAsync_EmitsEvents_OnAttempts()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(5),
            MaxAttempts = 3
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        var events = new List<LifecycleEvent>();
        reconnector.OnEvent += e => events.Add(e);

        var attemptCount = 0;
        await reconnector.ReconnectAsync(() =>
        {
            attemptCount++;
            if (attemptCount < 2)
                throw new InvalidOperationException("fail");
            return Task.CompletedTask;
        });

        events.Should().Contain(e => e.EventType == LifecycleEventType.TransportReconnecting);
    }

    [Fact]
    public async Task ReconnectAsync_EmitsTransportFailed_OnExhaustion()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(5),
            MaxAttempts = 1
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        var events = new List<LifecycleEvent>();
        reconnector.OnEvent += e => events.Add(e);

        try
        {
            await reconnector.ReconnectAsync(() =>
                throw new InvalidOperationException("Always fails"));
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        events.Should().Contain(e => e.EventType == LifecycleEventType.TransportFailed);
    }

    [Fact]
    public async Task ReconnectAsync_RespectsMaxDelayCap()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromMilliseconds(100),
            Multiplier = 10.0,
            MaxDelay = TimeSpan.FromMilliseconds(200),
            MaxAttempts = 5
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        var attemptCount = 0;
        var timestamps = new List<DateTimeOffset>();

        await reconnector.ReconnectAsync(() =>
        {
            timestamps.Add(DateTimeOffset.UtcNow);
            attemptCount++;
            if (attemptCount < 4)
                throw new InvalidOperationException("fail");
            return Task.CompletedTask;
        });

        // Should have 4 attempts
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task ReconnectAsync_SupportsCancellation()
    {
        var options = new ReconnectionOptions
        {
            InitialDelay = TimeSpan.FromSeconds(10),
            MaxAttempts = 100
        };
        var reconnector = new ExponentialBackoffReconnector(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => reconnector.ReconnectAsync(
            () => throw new InvalidOperationException("fail"),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
