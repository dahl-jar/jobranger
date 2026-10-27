using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Ads;

public sealed record AdShowAnyway(string Id, string AdId, bool On) : PayloadBase(Tag) {
    public const string Tag = "ad.show_anyway";
}
