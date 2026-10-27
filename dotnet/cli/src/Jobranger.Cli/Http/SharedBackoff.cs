namespace Jobranger.Cli.Http;

internal sealed class SharedBackoff(TimeProvider time) {
    private const int factor = 2;

    private static readonly TimeSpan firstPause = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan longestPause = TimeSpan.FromSeconds(120);

    private readonly Lock gate = new();
    private DateTimeOffset until = DateTimeOffset.MinValue;
    private TimeSpan step = firstPause;

    public Task WaitAsync(CancellationToken cancellationToken) {
        TimeSpan remaining;
        lock (gate) {
            remaining = until - time.GetUtcNow();
        }
        return remaining > TimeSpan.Zero ? Task.Delay(remaining, time, cancellationToken) : Task.CompletedTask;
    }

    public void Penalize(TimeSpan? retryAfter) {
        lock (gate) {
            TimeSpan pause = retryAfter is TimeSpan given && given > TimeSpan.Zero ? given : step;
            DateTimeOffset end = time.GetUtcNow() + pause;
            until = end > until ? end : until;
            step = step * factor < longestPause ? step * factor : longestPause;
        }
    }

    public void Reset() {
        lock (gate) {
            step = firstPause;
        }
    }
}
