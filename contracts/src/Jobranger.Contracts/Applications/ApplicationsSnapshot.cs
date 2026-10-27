using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Applications;

public sealed record ApplicationsSnapshot(
    string GeneratedAt,
    IReadOnlyList<ApplicationRow> Applications,
    long ApplicationsTotal) : IPayload {
    public const string Tag = "applications.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
