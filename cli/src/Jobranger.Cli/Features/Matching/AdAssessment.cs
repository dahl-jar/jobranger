namespace Jobranger.Cli.Features.Matching;

internal sealed record AdAssessment(
    bool IsProgrammingJob,
    string Label,
    string Summary,
    bool RequiresMaster,
    string? DisplayTitle,
    string? RoleType,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<Check> Checks,
    MatchAssessment Match);
