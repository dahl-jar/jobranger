using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunCancel(string Id, long RunId) : PayloadBase(Tag) {
    public const string Tag = "run.cancel";
}
