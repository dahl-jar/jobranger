namespace Jobranger.Cli.Features.Matching;

internal sealed record MatchAssessment(
    string Status,
    string Reason,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Blockers,
    bool IsLowCode,
    double? RequiredExperienceYears);
