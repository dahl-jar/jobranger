namespace Jobranger.Cli.Tests.Mocks;

internal static class TestWaits {
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(10);

    public static async Task UntilAsync(Func<bool> condition) {
        using CancellationTokenSource limit = new(Step);
        while (!condition()) {
            await Task.Delay(pollInterval, limit.Token);
        }
    }
}
