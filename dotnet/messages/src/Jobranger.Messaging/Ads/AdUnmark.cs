using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Ads;

public sealed record AdUnmark(string Id, string AdId) : PayloadBase(Tag) {
    public const string Tag = "ad.unmark";
}
