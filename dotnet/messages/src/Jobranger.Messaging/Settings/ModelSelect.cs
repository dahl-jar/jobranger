using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Settings;

public sealed record ModelSelect(string Id, string Provider, string Model) : PayloadBase(Tag) {
    public const string Tag = "model.select";
}
