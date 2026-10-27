using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record GuidanceUpdate(string Id, string Text) : PayloadBase(Tag) {
    public const string Tag = "guidance.update";
}
