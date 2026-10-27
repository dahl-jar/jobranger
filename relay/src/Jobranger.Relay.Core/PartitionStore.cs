using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core;

public sealed class PartitionStore : ISweepable, IDisposable
{
    private const int shardKeyBytes = 32;

    private readonly Func<RateLimiter> create;
    private readonly int maxPartitions;
    private readonly TimeProvider time;
    private readonly byte[] shardKey;
    private readonly RateLimiter[] overflow;
    private readonly Dictionary<string, RateLimiter> live = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private DateTimeOffset lastSweep;

    public PartitionStore(
        Func<RateLimiter> create,
        int maxPartitions,
        TimeProvider time,
        int overflowShards = RelayLimits.OverflowShards,
        byte[]? shardKey = null)
    {
        this.create = create;
        this.maxPartitions = maxPartitions;
        this.time = time;
        this.shardKey = shardKey ?? RandomNumberGenerator.GetBytes(shardKeyBytes);
        overflow = [.. Enumerable.Range(0, overflowShards).Select(_ => create())];
        lastSweep = time.GetUtcNow();
    }

    internal int LiveCount
    {
        get
        {
            lock (gate)
            {
                return live.Count;
            }
        }
    }

    public RateLimitLease AttemptAcquire(string key) => AttemptAcquire(key, key);

    public RateLimitLease AttemptAcquire(string key, string network)
    {
        lock (gate)
        {
            SweepIfDue();
            return LimiterFor(key, network).AttemptAcquire();
        }
    }

    public void Sweep()
    {
        lock (gate)
        {
            SweepIdle(time.GetUtcNow());
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (RateLimiter limiter in live.Values.Concat(overflow))
            {
                limiter.Dispose();
            }

            live.Clear();
        }
    }

    internal int ShardOf(string key)
    {
        byte[] digest = HMACSHA256.HashData(shardKey, Encoding.UTF8.GetBytes(key));
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(digest) % (uint)overflow.Length);
    }

    private RateLimiter LimiterFor(string key, string network)
    {
        if (live.TryGetValue(key, out RateLimiter? limiter))
        {
            return limiter;
        }

        if (live.Count >= maxPartitions)
        {
            return overflow[ShardOf(network)];
        }

        limiter = create();
        live.Add(key, limiter);
        return limiter;
    }

    private void SweepIfDue()
    {
        DateTimeOffset now = time.GetUtcNow();
        if (now - lastSweep < RelayLimits.PartitionSweepInterval)
        {
            return;
        }

        SweepIdle(now);
    }

    private void SweepIdle(DateTimeOffset now)
    {
        lastSweep = now;
        foreach ((string key, RateLimiter limiter) in live)
        {
            if (limiter.IdleDuration is not null)
            {
                live.Remove(key);
                limiter.Dispose();
            }
        }
    }
}
