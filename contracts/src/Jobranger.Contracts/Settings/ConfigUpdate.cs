using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Settings.Patches;

namespace Jobranger.Contracts.Settings;

public sealed record ConfigUpdate(string Id, ConfigPatch Patch) : PayloadBase(Tag) {
    public const string Tag = "config.update";
}
