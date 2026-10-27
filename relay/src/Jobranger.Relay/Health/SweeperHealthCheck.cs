using Jobranger.Relay.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Jobranger.Relay.Health;

internal sealed class SweeperHealthCheck(SweeperHeartbeat heartbeat, TimeProvider time, TimeSpan limit) : IHealthCheck {
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(heartbeat.BeatWithin(time.GetUtcNow(), limit)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"sweeper has not finished a sweep in {limit}"));
}
