using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Replies;

public sealed record Unpair(string Id) : PayloadBase(Tag) {
    public const string Tag = "unpair";
}
