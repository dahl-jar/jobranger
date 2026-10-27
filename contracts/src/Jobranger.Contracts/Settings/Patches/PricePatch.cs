using System.Text.Json.Serialization;

namespace Jobranger.Contracts.Settings.Patches;

public sealed record PricePatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? InputPerMtok = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? OutputPerMtok = null);
