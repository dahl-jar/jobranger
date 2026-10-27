using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
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
    string Language) : IPayload {
    public const string Tag = "config.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
