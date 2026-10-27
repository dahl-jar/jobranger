using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Applications;

public sealed record ApplicationsSnapshot(
    string GeneratedAt,
    IReadOnlyList<ApplicationRow> Applications,
    long ApplicationsTotal) : PayloadBase(Tag) {
    public const string Tag = "applications.snapshot";
}
