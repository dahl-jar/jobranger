using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Runs;

public sealed record RunStart(string Id) : PayloadBase(Tag) {
    public const string Tag = "run.start";
}
