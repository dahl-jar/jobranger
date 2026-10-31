using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Settings;

public sealed record GuidanceReset(string Id) : PayloadBase(Tag) {
    public const string Tag = "guidance.reset";
}
