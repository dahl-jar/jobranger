using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Learning;

public sealed record LearningForget(string Id, long DecisionId) : PayloadBase(Tag) {
    public const string Tag = "learning.forget";
}
