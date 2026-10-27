using System.Text.Json.Serialization;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Settings.Patches;

public sealed record ModelConfigPatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PricePatch? Price = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [property: JsonConverter(typeof(PatchValueConverter<double?>))]
    PatchValue<double?> MonthlyLimitUsd = default);
