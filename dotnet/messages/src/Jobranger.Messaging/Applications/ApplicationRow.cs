using Jobranger.Messaging.Ads;

namespace Jobranger.Messaging.Applications;

public sealed record ApplicationRow(
    long Id,
    string? AdId,
    string Company,
    string Title,
    string Place,
    string? RoleType,
    string? Source,
    string? Url,
    string? Deadline,
    string? Fit,
    string Status,
    string Note,
    string AppliedAt,
    string? FoundAt,
    string? CvName,
    IReadOnlyList<Check> Checks,
    IReadOnlyList<HistoryEntry> History,
    bool Removed);
