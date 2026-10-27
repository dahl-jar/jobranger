using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ContextRemove(string Id, string DocId) : PayloadBase(Tag) {
    public const string Tag = "context.remove";
}
