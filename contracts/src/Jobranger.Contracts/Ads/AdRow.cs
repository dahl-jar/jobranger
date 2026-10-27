namespace Jobranger.Contracts.Ads;

public sealed record AdRow(
    string Id,
    string Company,
    string Title,
    string Place,
    string RoleType,
    string Source,
    string? PublishedAt,
    string FoundAt,
    string Url,
    string? Deadline,
    string Fit,
    bool IsNew,
    bool Today,
    string Summary,
    string Reason,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<Check> Checks,
    string? Mark,
    string? MarkedAt,
    bool ShownAnyway);
