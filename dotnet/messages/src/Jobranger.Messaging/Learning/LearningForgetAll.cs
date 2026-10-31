using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Learning;

public sealed record LearningForgetAll(string Id) : PayloadBase(Tag) {
    public const string Tag = "learning.forget_all";
}
