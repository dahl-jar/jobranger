using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Applications;

public sealed record ApplicationRemove(string Id, long ApplicationId, bool Removed) : PayloadBase(Tag) {
    public const string Tag = "application.remove";
}
