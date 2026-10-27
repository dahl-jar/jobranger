using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Runs;

public sealed record RunCancelled(long RunId, string At) : PayloadBase(Tag) {
    public const string Tag = "run.cancelled";
}
