using System.Text.Json.Serialization;

namespace Jobranger.Contracts.Settings.Patches;

public sealed record SourcesPatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Nav = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Employers = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Finn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Linkedin = null);
