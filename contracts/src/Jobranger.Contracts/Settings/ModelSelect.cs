using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record ModelSelect(string Id, string Provider, string Model) : PayloadBase(Tag) {
    public const string Tag = "model.select";
}
