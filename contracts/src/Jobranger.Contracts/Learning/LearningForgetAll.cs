using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Learning;

public sealed record LearningForgetAll(string Id) : PayloadBase(Tag) {
    public const string Tag = "learning.forget_all";
}
