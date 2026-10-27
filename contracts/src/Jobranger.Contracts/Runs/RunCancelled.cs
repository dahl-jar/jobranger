using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunCancelled(long RunId, string At) : PayloadBase(Tag) {
    public const string Tag = "run.cancelled";
}
