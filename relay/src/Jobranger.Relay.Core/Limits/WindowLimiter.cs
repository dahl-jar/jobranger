using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core.Limits;

public sealed class WindowLimiter(int permits, TimeSpan window, TimeProvider time) : RateLimiter {
    private readonly Lock gate = new();
    private DateTimeOffset windowStart = time.GetUtcNow();
    private int used;

    public override TimeSpan? IdleDuration {
        get {
            lock (gate) {
                DateTimeOffset windowEnd = windowStart + window;
                DateTimeOffset now = time.GetUtcNow();
                return now >= windowEnd ? now - windowEnd : null;
            }
        }
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) {
        lock (gate) {
            DateTimeOffset now = time.GetUtcNow();
            if (now >= windowStart + window) {
                windowStart = now;
                used = 0;
            }

            if (used + permitCount > permits) {
                return new RefusedLease(windowStart + window - now);
            }

            used += permitCount;
            return GrantedLease.Instance;
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(permitCount));
}
