namespace Jobranger.Contracts.Ads;

public sealed record ClosedAd(
    string Id,
    string ClosedOn,
    string Company,
    string Title,
    string Source,
    string Reason,
    string Mark);
