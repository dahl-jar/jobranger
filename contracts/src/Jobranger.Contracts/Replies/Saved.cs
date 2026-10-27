using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Replies;

public sealed record Saved(
    string Ref,
    string AppliesFrom,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Recheck? Recheck = null) : IPayload {
    public const string Tag = "saved";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
