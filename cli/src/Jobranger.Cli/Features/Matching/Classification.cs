namespace Jobranger.Cli.Features.Matching;

internal sealed record Classification(
    string Label,
    string? Summary,
    bool RequiresMaster,
    bool IsProgrammingJob,
    string? DisplayTitle,
    string? RoleType,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<string> Requirements,
    Match? Match,
    IReadOnlyList<Check> Checks);
