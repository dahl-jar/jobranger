using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Runs;

public sealed record RunCancel(string Id, long RunId) : PayloadBase(Tag) {
    public const string Tag = "run.cancel";
}
