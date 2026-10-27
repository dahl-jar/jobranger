namespace Jobranger.Cli.Features.Link;

internal sealed class ReconnectBackoff {
    private const double firstCeilingSeconds = 1;
    private const double lastCeilingSeconds = 300;
    private const double growth = 2;

    private static readonly TimeSpan resetAfter = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan longestWait = TimeSpan.FromSeconds(lastCeilingSeconds);

    private int failures;

    public TimeSpan Next(TimeSpan connectedFor, double sample, TimeSpan retryAfter) {
        if (connectedFor >= resetAfter) {
            failures = 0;
        }
        double ceiling = Math.Min(lastCeilingSeconds, firstCeilingSeconds * Math.Pow(growth, failures));
        failures++;
        TimeSpan jittered = TimeSpan.FromSeconds(ceiling * sample);
        TimeSpan asked = retryAfter < longestWait ? retryAfter : longestWait;
        return asked > jittered ? asked : jittered;
    }
}
