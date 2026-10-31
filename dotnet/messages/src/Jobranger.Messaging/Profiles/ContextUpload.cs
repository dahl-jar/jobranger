using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Profiles;

public sealed record ContextUpload(string Id, string Name, string ContentB64) : PayloadBase(Tag) {
    public const string Tag = "context.upload";
}
