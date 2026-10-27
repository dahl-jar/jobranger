namespace Jobranger.Cli.Features.Ads;

internal sealed record StoredClassification(
    string Label,
    string? Summary,
    bool RequiresMaster,
    bool IsProgrammingJob,
    string? DisplayTitle,
    string? RoleType,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Requirements,
    string? Assessment);
