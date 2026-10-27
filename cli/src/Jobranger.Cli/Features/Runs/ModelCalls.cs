using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Runs;

internal sealed class ModelCalls(IReadOnlyList<AdView> representatives, SharedBackoff backoff) {
    private const int classifyConcurrency = 4;
    private const int maxConsecutiveFailures = 3;

    private readonly LinkedList<AdView> waiting = new(representatives);
    private readonly Dictionary<Task<ModelReply>, (AdView Representative, decimal ReservedUsd)> running = [];
    private int width = 1;
    private int failuresInARow;

    public int Running => running.Count;

    public decimal ReservedUsd => running.Values.Sum(call => call.ReservedUsd);

    public AdView Next => Enumerable.First(waiting);

    public bool HasWaiting => waiting.Count > 0;

    public bool CanStart => running.Count < width && waiting.Count > 0 && !backoff.Paused;

    public void Start(Task<ModelReply> call, decimal reservedUsd) {
        AdView representative = Next;
        waiting.RemoveFirst();
        running.Add(call, (representative, reservedUsd));
    }

    public async Task<(AdView Representative, Task<ModelReply> Call)> NextFinishedAsync() {
        Task<ModelReply> finished = await Task.WhenAny(running.Keys).ConfigureAwait(false);
        AdView representative = running[finished].Representative;
        running.Remove(finished);
        return (representative, finished);
    }

    public void Answered() {
        width = classifyConcurrency;
        failuresInARow = 0;
        backoff.Reset();
    }

    public bool TryRequeue(AdView representative, ApiException error) {
        if (ApiErrors.Pauses(error.Kind)) {
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

    public IReadOnlyList<ModelReply> Finished() =>
        [.. running.Keys.Where(call => call.Status == TaskStatus.RanToCompletion).Select(call => call.Result)];
}
