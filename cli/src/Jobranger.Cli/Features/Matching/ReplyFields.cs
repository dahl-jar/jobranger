using System.Text.Json;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class ReplyFields {
    public static Dictionary<string, JsonElement>? Object(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object) {
            return null;
        }
        Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject()) {
            fields[property.Name] = property.Value;
        }
        return fields;
    }

    public static bool HasExactly(Dictionary<string, JsonElement> fields, IReadOnlyCollection<string> keys) =>
        fields.Count == keys.Count && keys.All(fields.ContainsKey);

    public static IReadOnlyList<string>? Texts(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Array) {
            return null;
        }
        List<string> texts = [];
        foreach (JsonElement item in element.EnumerateArray()) {
            if (JsonText.String(item) is not string text) {
                return null;
            }
            texts.Add(text);
        }
        return texts;
    }

    public static bool? StrictFlag(JsonElement element) => element.ValueKind switch {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static double? StrictNumber(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double number) ? number : null;

    public static bool? LaxFlag(JsonElement element) {
        if (StrictFlag(element) is bool flag) {
            return flag;
        }
        if (StrictNumber(element) is double number) {
            return number switch {
                1 => true,
                0 => false,
                _ => null,
            };
        }
        string? text = JsonText.String(element);
        if (text is null) {
            return null;
        }
        return PydanticLax.Bool(text);
    }

    public static double? LaxNumber(JsonElement element) {
        if (StrictNumber(element) is double number) {
            return number;
        }
        if (StrictFlag(element) is bool flag) {
            return flag ? 1 : 0;
        }
        if (JsonText.String(element) is not string text) {
            return null;
        }
        return PydanticLax.Float(text);
    }
}
