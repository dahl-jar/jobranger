using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core;

public sealed class BucketLimiter : RateLimiter {
    private readonly int burst;
    private readonly int refill;
    private readonly TimeSpan period;
    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private DateTimeOffset lastRefill;
    private DateTimeOffset fullSince;
    private int tokens;

    public BucketLimiter(int burst, int refill, TimeSpan period, TimeProvider time) {
        this.burst = burst;
        this.refill = refill;
        this.period = period;
        this.time = time;
        tokens = burst;
        lastRefill = time.GetUtcNow();
        fullSince = lastRefill;
    }

    public override TimeSpan? IdleDuration {
        get {
            lock (gate) {
                DateTimeOffset now = Refill();
                return tokens == burst ? now - fullSince : null;
            }
        }
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) {
        lock (gate) {
            DateTimeOffset now = Refill();
            if (permitCount > tokens) {
                return new RefusedLease(lastRefill + period - now);
            }

            tokens -= permitCount;
            return GrantedLease.Instance;
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(permitCount));

    private DateTimeOffset Refill() {
        DateTimeOffset now = time.GetUtcNow();
        long periods = (now - lastRefill).Ticks / period.Ticks;
        long periodsToFull = (burst - tokens + refill - 1) / refill;
        if (tokens < burst && periods >= periodsToFull) {
            fullSince = lastRefill + (period * periodsToFull);
        }

        tokens = (int)Math.Min(burst, tokens + (periods * refill));
        lastRefill += period * periods;
        return now;
    }
}
