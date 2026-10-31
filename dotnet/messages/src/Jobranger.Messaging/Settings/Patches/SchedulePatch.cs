using System.Text.Json.Serialization;

namespace Jobranger.Messaging.Settings.Patches;

public sealed record SchedulePatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Times = null);
