using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings;

public sealed record GuidanceReset(string Id) : PayloadBase(Tag) {
    public const string Tag = "guidance.reset";
}
