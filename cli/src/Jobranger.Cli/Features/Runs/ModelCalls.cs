using System.Collections.Frozen;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Runs;

internal sealed class ModelCalls(IReadOnlyList<AdView> representatives, SharedBackoff backoff) {
    private const int classifyConcurrency = 4;
    private const int maxConsecutiveFailures = 3;

    private static readonly FrozenSet<ApiErrorKind> pausingKinds =
        FrozenSet.Create(ApiErrorKind.RateLimit, ApiErrorKind.Overloaded);

    private readonly LinkedList<AdView> waiting = new(representatives);
    private readonly Dictionary<Task<ModelReply>, AdView> running = [];
    private int width = 1;
    private int failuresInARow;

    public int Running => running.Count;

    public bool HasWaiting => waiting.Count > 0;

    public bool CanStart => running.Count < width && waiting.Count > 0 && !backoff.Paused;

    public void Start(Func<AdView, Task<ModelReply>> call) {
        AdView representative = Enumerable.First(waiting);
        waiting.RemoveFirst();
        running.Add(call(representative), representative);
    }

    public async Task<(AdView Representative, Task<ModelReply> Call)> NextFinishedAsync() {
        Task<ModelReply> finished = await Task.WhenAny(running.Keys).ConfigureAwait(false);
        AdView representative = running[finished];
        running.Remove(finished);
        return (representative, finished);
    }

    public void Answered() {
        width = classifyConcurrency;
        failuresInARow = 0;
        backoff.Reset();
    }

    public bool TryRequeue(AdView representative, ApiException error) {
        if (pausingKinds.Contains(error.Kind)) {
            backoff.Penalize(error.RetryAfter);
        }
        failuresInARow++;
        if (failuresInARow >= maxConsecutiveFailures) {
            return false;
        }
        waiting.AddFirst(representative);
        return true;
    }

    public Task PauseAsync(CancellationToken cancellationToken) => backoff.WaitAsync(cancellationToken);

    public Task WhenAllDone() => Task.WhenAll(running.Keys.Cast<Task>());
}
