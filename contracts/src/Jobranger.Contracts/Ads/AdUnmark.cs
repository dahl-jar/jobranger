using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Ads;

public sealed record AdUnmark(string Id, string AdId) : IPayload {
    public const string Tag = "ad.unmark";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
