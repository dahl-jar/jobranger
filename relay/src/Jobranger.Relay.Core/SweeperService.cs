using Microsoft.Extensions.Hosting;

namespace Jobranger.Relay.Core;

public sealed class SweeperService(IEnumerable<ISweepable> stores, SweeperHeartbeat heartbeat, TimeProvider time) : BackgroundService
{
    private readonly ISweepable[] stores = [.. stores];
    private readonly PeriodicTimer timer = new(RelayLimits.SweepPeriod, time);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        heartbeat.Beat(time.GetUtcNow());
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (ISweepable store in stores)
            {
                store.Sweep();
            }

            heartbeat.Beat(time.GetUtcNow());
        }
    }
}
