using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunFinished(
    long RunId,
    string FinishedAt,
    long AdsRead,
    long NewAds,
    long Matches,
    long Hidden,
    double CostUsd,
    IReadOnlyList<string> FailedSources,
    bool BudgetPaused) : PayloadBase(Tag) {
    public const string Tag = "run.finished";
}
