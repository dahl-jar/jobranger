using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Applications;

public sealed record ApplicationCreate(string Id, string Company, string Title, string Note) : PayloadBase(Tag) {
    public const string Tag = "application.create";
}
