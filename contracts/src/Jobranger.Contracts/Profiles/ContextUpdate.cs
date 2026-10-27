using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ContextUpdate(string Id, string About) : PayloadBase(Tag) {
    public const string Tag = "context.update";
}
