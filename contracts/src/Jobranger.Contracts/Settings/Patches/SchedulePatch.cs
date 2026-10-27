using System.Text.Json.Serialization;

namespace Jobranger.Contracts.Settings.Patches;

public sealed record SchedulePatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Times = null);
