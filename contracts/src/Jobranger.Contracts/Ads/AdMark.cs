using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Ads;

public sealed record AdMark(string Id, string AdId, string Mark) : PayloadBase(Tag) {
    public const string Tag = "ad.mark";
}
