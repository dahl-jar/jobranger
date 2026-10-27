using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunProgress(
    long RunId,
    string Mode,
    string StartedAt,
    int Percent,
    IReadOnlyList<SourceStep> Sources,
    ModelStep Model) : PayloadBase(Tag) {
    public const string Tag = "run.progress";
}
