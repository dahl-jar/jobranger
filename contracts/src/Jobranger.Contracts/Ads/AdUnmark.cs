using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Ads;

public sealed record AdUnmark(string Id, string AdId) : PayloadBase(Tag) {
    public const string Tag = "ad.unmark";
}
