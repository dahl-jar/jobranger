using System.Text.Json.Serialization;
using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Settings.Patches;

public sealed record EmailNotifyPatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? On = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [property: JsonConverter(typeof(PatchValueConverter<string>))]
    PatchValue<string?> Address = default);
