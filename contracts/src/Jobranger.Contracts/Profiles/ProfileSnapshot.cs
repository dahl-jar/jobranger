using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ProfileSnapshot(
    string? CvName,
    string? CvReadAt,
    string Summary,
    string Education,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Stack,
    IReadOnlyList<ProjectRef> Projects) : IPayload {
    public const string Tag = "profile.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
