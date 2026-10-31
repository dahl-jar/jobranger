using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Profiles;

public sealed record ContextRemove(string Id, string DocId) : PayloadBase(Tag) {
    public const string Tag = "context.remove";
}
