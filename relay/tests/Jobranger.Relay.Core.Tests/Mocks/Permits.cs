using System.Threading.RateLimiting;

namespace Jobranger.Relay.Core.Tests.Mocks;

internal static class Permits {
    public static void Take(this RateLimiter limiter, int count) {
        for (int take = 0; take < count; take++) {
            using RateLimitLease lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }
    }
}
