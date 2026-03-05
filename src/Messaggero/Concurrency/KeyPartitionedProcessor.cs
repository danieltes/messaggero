using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace Messaggero.Concurrency;

/// <summary>
/// Dispatches work items to fixed partitions based on a string key.
/// Messages with the same key are always processed sequentially on the same partition.
/// Messages with different keys may be processed concurrently across partitions.
/// Null keys are distributed round-robin across partitions.
/// </summary>
/// <typeparam name="TState">The type of state passed to work items (not used for partitioning).</typeparam>
public sealed class KeyPartitionedProcessor<TState> : IAsyncDisposable
{
    private readonly Channel<Func<CancellationToken, Task>>[] _partitions;
    private readonly Task[] _consumers;
    private readonly CancellationTokenSource _cts = new();
    private int _roundRobinCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyPartitionedProcessor{TState}"/> class.
    /// </summary>
    /// <param name="concurrencyLimit">The number of partitions (and concurrent workers).</param>
    public KeyPartitionedProcessor(int concurrencyLimit)
    {
        if (concurrencyLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(concurrencyLimit), "Must be at least 1.");

        PartitionCount = concurrencyLimit;
        _partitions = new Channel<Func<CancellationToken, Task>>[concurrencyLimit];
        _consumers = new Task[concurrencyLimit];

        for (var i = 0; i < concurrencyLimit; i++)
        {
            _partitions[i] = Channel.CreateUnbounded<Func<CancellationToken, Task>>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

            var partitionIndex = i;
            _consumers[i] = Task.Factory.StartNew(
                () => ConsumePartitionAsync(_partitions[partitionIndex].Reader, _cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>
    /// Gets the number of partitions.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Enqueues a work item to the partition determined by the key.
    /// </summary>
    /// <param name="key">The partition key. Null keys round-robin across partitions.</param>
    /// <param name="work">The async work to execute.</param>
    public ValueTask EnqueueAsync(string? key, Func<CancellationToken, Task> work)
    {
        var index = key is null
            ? Interlocked.Increment(ref _roundRobinCounter) % PartitionCount
            : GetPartitionIndex(key);

        // Ensure index is positive
        if (index < 0) index += PartitionCount;

        return _partitions[index].Writer.WriteAsync(work);
    }

    /// <summary>
    /// Gets the partition index for a given key using MurmurHash3.
    /// </summary>
    /// <param name="key">The key to hash.</param>
    /// <returns>The partition index (0 to PartitionCount-1).</returns>
    public int GetPartitionIndex(string key)
    {
        var hash = MurmurHash3(Encoding.UTF8.GetBytes(key));
        return (int)(hash % (uint)PartitionCount);
    }

    /// <summary>
    /// MurmurHash3 32-bit implementation for deterministic, well-distributed hashing.
    /// </summary>
    private static uint MurmurHash3(byte[] key, uint seed = 0)
    {
        var length = key.Length;
        var nblocks = length / 4;
        var h1 = seed;

        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        // Body
        for (var i = 0; i < nblocks; i++)
        {
            var k1 = BitConverter.ToUInt32(key, i * 4);
            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;

            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        // Tail
        var tail = nblocks * 4;
        uint k = 0;
        switch (length & 3)
        {
            case 3: k ^= (uint)key[tail + 2] << 16; goto case 2;
            case 2: k ^= (uint)key[tail + 1] << 8; goto case 1;
            case 1:
                k ^= key[tail];
                k *= c1;
                k = RotateLeft(k, 15);
                k *= c2;
                h1 ^= k;
                break;
        }

        // Finalization
        h1 ^= (uint)length;
        h1 ^= h1 >> 16;
        h1 *= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *= 0xc2b2ae35;
        h1 ^= h1 >> 16;

        return h1;
    }

    private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));

    /// <summary>
    /// Signals all partitions to complete and waits for all in-flight work to drain.
    /// </summary>
    public async Task StopAsync()
    {
        // Complete all writers so consumers drain remaining items
        foreach (var partition in _partitions)
        {
            partition.Writer.Complete();
        }

        // Wait for all consumers to finish processing
        await Task.WhenAll(_consumers).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels all processing and disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        foreach (var partition in _partitions)
        {
            partition.Writer.TryComplete();
        }

        try
        {
            await Task.WhenAll(_consumers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
    }

    private static async Task ConsumePartitionAsync(
        ChannelReader<Func<CancellationToken, Task>> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var work in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
    }
}
