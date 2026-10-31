using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Learning;

public sealed record LearningForget(string Id, long DecisionId) : PayloadBase(Tag) {
    public const string Tag = "learning.forget";
}
