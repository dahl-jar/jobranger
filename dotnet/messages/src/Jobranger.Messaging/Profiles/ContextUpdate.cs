using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Profiles;

public sealed record ContextUpdate(string Id, string About) : PayloadBase(Tag) {
    public const string Tag = "context.update";
}
