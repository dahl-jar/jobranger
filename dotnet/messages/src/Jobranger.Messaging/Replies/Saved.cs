using System.Text.Json.Serialization;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Replies;

public sealed record Saved(
    string Ref,
    string AppliesFrom,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Recheck? Recheck = null) : PayloadBase(Tag) {
    public const string Tag = "saved";
}
