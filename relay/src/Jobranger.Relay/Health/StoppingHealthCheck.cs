using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Jobranger.Relay.Health;

internal sealed class StoppingHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck {
    private const string stoppingMessage = "relay is shutting down";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(lifetime.ApplicationStopping.IsCancellationRequested
            ? HealthCheckResult.Unhealthy(stoppingMessage)
            : HealthCheckResult.Healthy());
}
