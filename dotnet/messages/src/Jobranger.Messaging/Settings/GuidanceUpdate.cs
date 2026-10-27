using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Settings;

public sealed record GuidanceUpdate(string Id, string Text) : PayloadBase(Tag) {
    public const string Tag = "guidance.update";
}
