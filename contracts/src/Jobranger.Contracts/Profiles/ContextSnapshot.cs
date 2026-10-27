using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ContextSnapshot(
    string About,
    long AboutLimit,
    IReadOnlyList<ContextDoc> Docs,
    long DocsLimit) : IPayload {
    public const string Tag = "context.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
