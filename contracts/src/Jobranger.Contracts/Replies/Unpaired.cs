using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Replies;

public sealed record Unpaired(string Reason) : PayloadBase(Tag) {
    public const string Tag = "unpaired";
}
