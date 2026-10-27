using System.Text.Json.Serialization;

namespace Jobranger.Contracts.Settings.Patches;

public sealed record NotifyPatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Website = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EmailNotifyPatch? Email = null);
