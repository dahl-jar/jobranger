using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Ads;

public sealed record AdShowAnyway(string Id, string AdId, bool On) : PayloadBase(Tag) {
    public const string Tag = "ad.show_anyway";
}
