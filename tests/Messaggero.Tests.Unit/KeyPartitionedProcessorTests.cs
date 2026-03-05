using FluentAssertions;

namespace Messaggero.Tests.Unit;

public class KeyPartitionedProcessorTests
{
    [Fact]
    public async Task SameKey_MessagesProcessedSequentially()
    {
        var concurrency = 4;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);
        var order = new List<int>();
        var semaphore = new SemaphoreSlim(0);

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            await processor.EnqueueAsync("same-key", async ct =>
            {
                await Task.Delay(10, ct);
                lock (order) { order.Add(index); }
                semaphore.Release();
            });
        }

        // Wait for all to complete
        for (var i = 0; i < 5; i++)
            await semaphore.WaitAsync(TimeSpan.FromSeconds(5));

        order.Should().BeInAscendingOrder("same-key messages must be processed in order");
    }

    [Fact]
    public async Task DifferentKeys_MessagesProcessedConcurrently()
    {
        var concurrency = 4;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var locker = new object();
        var allDone = new CountdownEvent(4);

        for (var i = 0; i < 4; i++)
        {
            var key = $"key-{i}";
            await processor.EnqueueAsync(key, async ct =>
            {
                lock (locker)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(100, ct);

                lock (locker) { concurrentCount--; }
                allDone.Signal();
            });
        }

        allDone.Wait(TimeSpan.FromSeconds(10));
        maxConcurrent.Should().BeGreaterThan(1, "different keys should be processed concurrently");
    }

    [Fact]
    public async Task NullKey_RoundRobinsAcrossPartitions()
    {
        var concurrency = 4;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);
        var partitionsHit = new HashSet<int>();
        var semaphore = new SemaphoreSlim(0);

        for (var i = 0; i < 8; i++)
        {
            await processor.EnqueueAsync(null, async ct =>
            {
                lock (partitionsHit)
                {
                    partitionsHit.Add(Environment.CurrentManagedThreadId);
                }
                await Task.Delay(50, ct);
                semaphore.Release();
            });
        }

        for (var i = 0; i < 8; i++)
            await semaphore.WaitAsync(TimeSpan.FromSeconds(10));

        // With 4 partitions and 8 messages, at least 2 partitions should be used
        partitionsHit.Count.Should().BeGreaterThanOrEqualTo(2,
            "null-key messages should be distributed across partitions");
    }

    [Fact]
    public void PartitionCount_EqualsConcurrencyLimit()
    {
        var concurrency = 8;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);

        processor.PartitionCount.Should().Be(concurrency);
    }

    [Fact]
    public async Task ConsistentHashing_SameKeyAlwaysSamePartition()
    {
        var concurrency = 4;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);

        var partition1 = processor.GetPartitionIndex("customer-42");
        var partition2 = processor.GetPartitionIndex("customer-42");
        var partition3 = processor.GetPartitionIndex("customer-42");

        partition1.Should().Be(partition2).And.Be(partition3);
    }

    [Fact]
    public async Task StopAsync_DrainsAllPartitions()
    {
        var concurrency = 2;
        var processor = new Concurrency.KeyPartitionedProcessor<string>(concurrency);
        var completed = 0;

        for (var i = 0; i < 4; i++)
        {
            var key = $"key-{i % 2}";
            await processor.EnqueueAsync(key, async ct =>
            {
                await Task.Delay(50, ct);
                Interlocked.Increment(ref completed);
            });
        }

        await processor.StopAsync();
        completed.Should().Be(4, "all messages should be processed before stop completes");
    }
}
