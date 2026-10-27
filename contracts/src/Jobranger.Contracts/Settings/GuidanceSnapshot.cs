using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record GuidanceSnapshot(string Text, bool IsDefault, long Limit) : PayloadBase(Tag) {
    public const string Tag = "guidance.snapshot";
}
