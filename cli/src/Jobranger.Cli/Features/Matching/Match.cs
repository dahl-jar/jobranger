namespace Jobranger.Cli.Features.Matching;

internal sealed record Match(
    string Status,
    string Reason,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Blockers,
    bool IsLowCode,
    double? RequiredExperienceYears,
    string Version) {
    public bool Equals(Match? other) =>
        other is not null
        && Status == other.Status
        && Reason == other.Reason
        && Evidence.SequenceEqual(other.Evidence)
        && Gaps.SequenceEqual(other.Gaps)
        && Blockers.SequenceEqual(other.Blockers)
        && IsLowCode == other.IsLowCode
        && RequiredExperienceYears == other.RequiredExperienceYears
        && Version == other.Version;

    public override int GetHashCode() => HashCode.Combine(Status, Reason, IsLowCode, RequiredExperienceYears, Version);
}
