using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Ads;

public sealed record AdMark(string Id, string AdId, string Mark) : PayloadBase(Tag) {
    public const string Tag = "ad.mark";
}
