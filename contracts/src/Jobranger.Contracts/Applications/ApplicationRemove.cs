using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Applications;

public sealed record ApplicationRemove(string Id, long ApplicationId, bool Removed) : PayloadBase(Tag) {
    public const string Tag = "application.remove";
}
