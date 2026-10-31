using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Http;

internal static class LimiterTiming {
    private static readonly TimeSpan rest = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan settle = TimeSpan.FromSeconds(5);

    public static async Task<(bool Early, bool OnTime)> StateAroundAsync(
        FakeTimeProvider time, Task waiting, TimeSpan due) {
        time.Advance(due - rest);
        bool early = waiting.IsCompleted;
        time.Advance(rest);
        await waiting.WaitAsync(settle, TestContext.Current.CancellationToken);
        return (early, waiting.IsCompletedSuccessfully);
    }
}
