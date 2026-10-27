using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Replies;

public sealed record Unpaired(string Reason) : PayloadBase(Tag) {
    public const string Tag = "unpaired";
}
