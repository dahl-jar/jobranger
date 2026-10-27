using System.Text.Json.Serialization;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Applications;

public sealed record ApplicationUpdate(
    string Id,
    long ApplicationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null) : PayloadBase(Tag) {
    public const string Tag = "application.update";
}
