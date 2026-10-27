namespace Jobranger.Cli.Features.Runs;

internal interface IRunStore {
    long Start(DateTimeOffset startedAt);

    void Finish(long runId, RunSummary summary);

    RunRow? Last();

    IReadOnlyList<RunRow> List(int limit);
}
