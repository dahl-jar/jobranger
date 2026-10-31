using System.Threading.Channels;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Logging;
using SourcesEntry = Jobranger.Cli.Features.Sources.Sources;

namespace Jobranger.Cli.Features.Runs;

internal sealed class FetchStage(TimeProvider time, IRunEvents events, TimeSpan cancelGrace) {
    private const string deadlineError = "deadline";

    private static readonly TimeSpan defaultCancelGrace = TimeSpan.FromSeconds(2);

    public FetchStage(TimeProvider time, IRunEvents events)
        : this(time, events, defaultCancelGrace) {
    }

    public async Task<FetchTally> RunAsync(
        IReadOnlyList<ISource> sources, FetchContext context, IBatchWriter writer, CancellationToken runToken) {
        Channel<SourceOutcome> outcomes = Channel.CreateUnbounded<SourceOutcome>(
            new UnboundedChannelOptions { SingleReader = true });
        foreach (ISource source in sources) {
            events.Emit(new RunEvent(RunStage.SourceStarted, source.Name));
        }
        using CancellationTokenSource stage = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        Task[] fetches = [.. sources.Select(source =>
            Task.Run(() => FetchOneAsync(source, context, outcomes.Writer, stage.Token), CancellationToken.None))];
        FetchTally tally = new(0, 0, [], [], false);
        try {
            for (int received = 0; received < sources.Count; received++) {
                SourceOutcome outcome = await outcomes.Reader.ReadAsync(CancellationToken.None).ConfigureAwait(false);
                tally = Record(tally, outcome, writer);
            }
        } catch {
            await stage.CancelAsync().ConfigureAwait(false);
            throw;
        } finally {
            await Task.WhenAll(fetches).ConfigureAwait(false);
        }
        return tally;
    }

    private async Task FetchOneAsync(
        ISource source, FetchContext context, ChannelWriter<SourceOutcome> outcomes, CancellationToken stageToken) {
        using CancellationTokenSource deadline = new(SourceDeadline.For(source.Name), time);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, stageToken);
        SourceOutcome outcome = await AwaitWithinDeadlineAsync(
            SourcesEntry.FetchAsync(source, context, linked.Token), source.Name, linked.Token).ConfigureAwait(false);
        if (outcome.Cancelled && !stageToken.IsCancellationRequested) {
            LogEvents.SourceDeadlinePassed(context.Logger, source.Name, outcome.Batch?.Listings.Count ?? 0);
            outcome = outcome with { Error = deadlineError, Cancelled = false };
        }
        await outcomes.WriteAsync(outcome, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<SourceOutcome> AwaitWithinDeadlineAsync(
        Task<SourceOutcome> fetching, string source, CancellationToken token) {
        try {
            return await fetching.WaitAsync(token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return await AwaitGraceAsync(fetching, source).ConfigureAwait(false);
        }
    }

    private async Task<SourceOutcome> AwaitGraceAsync(Task<SourceOutcome> fetching, string source) {
        try {
            return await fetching.WaitAsync(cancelGrace, time).ConfigureAwait(false);
        } catch (TimeoutException) {
            return new SourceOutcome(source, null, null, true);
        }
    }

    private FetchTally Record(FetchTally tally, SourceOutcome outcome, IBatchWriter writer) {
        int found = 0;
        if (outcome.Batch is SourceBatch batch) {
            found = writer.Write(batch);
            tally = tally with { AdsRead = tally.AdsRead + batch.Read, NewAds = tally.NewAds + found };
        }
        if (outcome.Error is not null) {
            events.Emit(new RunEvent(RunStage.SourceFailed, outcome.Source));
            return tally with { Failed = [.. tally.Failed, outcome.Source] };
        }
        if (outcome.Cancelled) {
            return tally with { Cancelled = true };
        }
        events.Emit(new RunEvent(RunStage.SourceDone, outcome.Source, FoundNew: found));
        return tally with { SourcesRead = [.. tally.SourcesRead, outcome.Source] };
    }
}
