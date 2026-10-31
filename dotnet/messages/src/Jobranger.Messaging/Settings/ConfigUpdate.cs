using Jobranger.Messaging.Payloads;
using Jobranger.Messaging.Settings.Patches;

namespace Jobranger.Messaging.Settings;

public sealed record ConfigUpdate(string Id, ConfigPatch Patch) : PayloadBase(Tag) {
    public const string Tag = "config.update";
}
