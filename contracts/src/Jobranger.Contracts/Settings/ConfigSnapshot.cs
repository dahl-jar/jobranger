using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Settings.Config;

namespace Jobranger.Contracts.Settings;

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
