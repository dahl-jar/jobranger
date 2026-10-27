using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Runs;

public sealed record RunStart(string Id) : PayloadBase(Tag) {
    public const string Tag = "run.start";
}
