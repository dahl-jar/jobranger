using System.Text.Json.Serialization;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Profiles;

public sealed record ProfileUpdate(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Summary = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Education = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Languages = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Stack = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProjectEdit>? Projects = null) : PayloadBase(Tag) {
    public const string Tag = "profile.update";
}
