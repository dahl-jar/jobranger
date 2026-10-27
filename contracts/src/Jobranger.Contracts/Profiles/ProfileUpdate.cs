using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ProfileUpdate(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Summary = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Education = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Languages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Stack = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProjectEdit>? Projects = null) : IPayload {
    public const string Tag = "profile.update";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
