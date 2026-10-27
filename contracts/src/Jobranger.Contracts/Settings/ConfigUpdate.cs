using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Settings.Patches;

namespace Jobranger.Contracts.Settings;

public sealed record ConfigUpdate(string Id, ConfigPatch Patch) : IPayload {
    public const string Tag = "config.update";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
