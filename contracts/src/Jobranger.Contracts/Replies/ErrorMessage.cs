using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Replies;

public sealed record ErrorMessage(string? Ref, string Code, string Message, string? Field) : PayloadBase(Tag) {
    public const string Tag = "error";
}
