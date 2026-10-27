using System.Text.Json.Serialization;
using Jobranger.Contracts.Limits;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Ads;

public sealed record AdsSnapshot(
    string GeneratedAt,
    string AsOf,
    IReadOnlyList<AdRow> Active,
    long ActiveTotal,
    IReadOnlyList<HiddenAd> Hidden,
    long HiddenTotal,
    HiddenCounts HiddenCounts,
    IReadOnlyList<ClosedAd> Closed,
    long ClosedTotal) : IPayload {
    public const string Tag = "ads.snapshot";

    [JsonPropertyOrder(PayloadOrder.Type)]
    public string Type => Tag;

    [JsonPropertyOrder(PayloadOrder.Version)]
    public int V => ContractLimits.ContractVersion;
}
