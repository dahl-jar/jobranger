using Jobranger.Messaging.Payloads;
using Jobranger.Messaging.Settings.Config;

namespace Jobranger.Messaging.Settings;

public sealed record ConfigSnapshot(
    IReadOnlyList<RoleOption> Roles,
    Targets Targets,
    Sources Sources,
    Schedule Schedule,
    Notify Notify,
    ModelConfig Model,
    string Language) : PayloadBase(Tag) {
    public const string Tag = "config.snapshot";
}
