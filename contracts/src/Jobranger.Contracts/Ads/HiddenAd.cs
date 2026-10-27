namespace Jobranger.Contracts.Ads;

public sealed record HiddenAd(
    string Id,
    string Company,
    string Title,
    string Place,
    string? RoleType,
    string Source,
    string Url,
    string FoundAt,
    bool Today,
    string Kind,
    string Reason);
