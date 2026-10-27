using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Replies;

public sealed record Unpair(string Id) : PayloadBase(Tag) {
    public const string Tag = "unpair";
}
