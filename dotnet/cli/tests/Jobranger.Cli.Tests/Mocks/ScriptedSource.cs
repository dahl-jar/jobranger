using Jobranger.Cli.Features.Sources;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class ScriptedSource(string name, Func<CancellationToken, Task<SourceBatch>> fetch) : ISource {
    private static readonly TimeSpan stopDelay = TimeSpan.FromMilliseconds(100);

    public string Name => name;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken Token { get; private set; }

    public static ScriptedSource Stalled(string name, SourceBatch partial) => new(name, async cancellationToken => {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await Task.Delay(stopDelay, CancellationToken.None).ConfigureAwait(false);
        return partial with { Partial = true };
    });

    public static ScriptedSource Done(string name, SourceBatch batch) => new(name, _ => Task.FromResult(batch));

    public async Task<SourceBatch> FetchAsync(FetchContext context, CancellationToken cancellationToken) {
        Token = cancellationToken;
        Started.TrySetResult();
        try {
            return await fetch(cancellationToken).ConfigureAwait(false);
        } finally {
            Finished.TrySetResult();
        }
    }
}
