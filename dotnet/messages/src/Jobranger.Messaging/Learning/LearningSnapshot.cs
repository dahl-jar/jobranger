using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Learning;

public sealed record LearningSnapshot(
    string State,
    string? UnavailableReason,
    long DecisionCount,
    int ColdStartThreshold,
    IReadOnlyList<LearningExample> Examples,
    long ExamplesTotal) : PayloadBase(Tag) {
    public const string Tag = "learning.snapshot";
}
