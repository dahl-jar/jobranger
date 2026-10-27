using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ProfileSnapshot(
    string? CvName,
    string? CvReadAt,
    string Summary,
    string Education,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Stack,
    IReadOnlyList<ProjectRef> Projects) : PayloadBase(Tag) {
    public const string Tag = "profile.snapshot";
}
