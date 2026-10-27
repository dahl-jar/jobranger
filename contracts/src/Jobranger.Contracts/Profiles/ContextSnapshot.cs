using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record ContextSnapshot(
    string About,
    long AboutLimit,
    IReadOnlyList<ContextDoc> Docs,
    long DocsLimit) : PayloadBase(Tag) {
    public const string Tag = "context.snapshot";
}
