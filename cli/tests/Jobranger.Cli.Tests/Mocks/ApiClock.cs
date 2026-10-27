using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal static class ApiClock {
    private const int maxSteps = 4000;

    private static readonly DateTimeOffset start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan step = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan rest = TimeSpan.FromMilliseconds(2);

    public static FakeTimeProvider Create() => new(start);

    public static async Task<T> RunAsync<T>(FakeTimeProvider time, Task<T> running) {
        for (int count = 0; count < maxSteps && !running.IsCompleted; count++) {
            await Task.WhenAny(running, Task.Delay(rest, TestContext.Current.CancellationToken)).ConfigureAwait(false);
            if (!running.IsCompleted) {
                time.Advance(step);
            }
        }
        Assert.True(running.IsCompleted, "the call never finished on the fake clock");
        return await running.ConfigureAwait(false);
    }
}
