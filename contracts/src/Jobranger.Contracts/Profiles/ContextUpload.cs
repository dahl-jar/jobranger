using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ContextUpload(string Id, string Name, string ContentB64) : PayloadBase(Tag) {
    public const string Tag = "context.upload";
}
