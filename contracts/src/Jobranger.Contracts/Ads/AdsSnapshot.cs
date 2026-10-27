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
    long ClosedTotal) : PayloadBase(Tag) {
    public const string Tag = "ads.snapshot";
}
