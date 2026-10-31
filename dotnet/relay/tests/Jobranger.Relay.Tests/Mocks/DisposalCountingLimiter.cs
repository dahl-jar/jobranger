using System.Threading.RateLimiting;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class DisposalCountingLimiter(RateLimiter inner, Action onDispose) : RateLimiter {
    public override TimeSpan? IdleDuration => inner.IdleDuration;

    public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => inner.AttemptAcquire(permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        inner.AcquireAsync(permitCount, cancellationToken);

    protected override void Dispose(bool disposing) {
        if (disposing) {
            onDispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
