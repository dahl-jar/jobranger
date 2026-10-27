namespace Jobranger.Relay.Tests;

internal static class SweeperTiming
{
    public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan DeadAfter = TimeSpan.FromMinutes(5);
}
