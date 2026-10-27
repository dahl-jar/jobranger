using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Applications;

public sealed record ApplicationCreate(string Id, string Company, string Title, string Note) : PayloadBase(Tag) {
    public const string Tag = "application.create";
}
