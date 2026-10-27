using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Applications;

public sealed record ApplicationsSnapshot(
    string GeneratedAt,
    IReadOnlyList<ApplicationRow> Applications,
    long ApplicationsTotal) : PayloadBase(Tag) {
    public const string Tag = "applications.snapshot";
}
