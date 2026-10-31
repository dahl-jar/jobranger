using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Replies;

public sealed record ErrorMessage(string? Ref, string Code, string Message, string? Field) : PayloadBase(Tag) {
    public const string Tag = "error";
}
