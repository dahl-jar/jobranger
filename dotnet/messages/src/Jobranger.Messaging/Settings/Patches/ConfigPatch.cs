using System.Text.Json.Serialization;

namespace Jobranger.Messaging.Settings.Patches;

public sealed record ConfigPatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TargetsPatch? Targets = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SourcesPatch? Sources = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SchedulePatch? Schedule = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NotifyPatch? Notify = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelConfigPatch? Model = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Language = null);
