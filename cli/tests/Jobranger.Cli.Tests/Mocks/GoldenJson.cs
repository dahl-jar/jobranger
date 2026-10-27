using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jobranger.Cli.Tests.Mocks;

internal static class GoldenJson {
    private const string roundTrip = "R";

    public static string Row(params (string Name, object? Value)[] fields) => Canonical(Object(fields));

    public static string Canonical(JsonElement element, string? skipped = null) {
        StringBuilder text = new();
        Append(text, element, skipped);
        return text.ToString();
    }

    public static JsonObject Object(params (string Name, object? Value)[] fields) {
        JsonObject row = [];
        foreach ((string name, object? value) in fields) {
            row[name] = Node(value);
        }
        return row;
    }

    private static string Canonical(JsonNode node) {
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        return Canonical(document.RootElement);
    }

    private static JsonNode? Node(object? value) => value switch {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        long number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        JsonNode node => node,
        IEnumerable<string> items => new JsonArray([.. items.Select(item => (JsonNode?)JsonValue.Create(item))]),
        IEnumerable<JsonNode> nodes => new JsonArray([.. nodes]),
        _ => throw new ArgumentException("no golden JSON form for type " + value.GetType().Name, nameof(value)),
    };

    private static void Append(StringBuilder text, JsonElement element, string? skipped) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                text.Append('{');
                foreach (JsonProperty property in element.EnumerateObject()
                    .Where(property => property.Name != skipped)
                    .OrderBy(property => property.Name, StringComparer.Ordinal)) {
                    text.Append(Quoted(property.Name)).Append(':');
                    Append(text, property.Value, null);
                    text.Append(',');
                }
                text.Append('}');
                break;
            case JsonValueKind.Array:
                text.Append('[');
                foreach (JsonElement item in element.EnumerateArray()) {
                    Append(text, item, null);
                    text.Append(',');
                }
                text.Append(']');
                break;
            case JsonValueKind.String:
                text.Append(Quoted(element.GetString()!));
                break;
            case JsonValueKind.Number:
                text.Append(element.GetDouble().ToString(roundTrip, CultureInfo.InvariantCulture));
                break;
            default:
                text.Append(element.GetRawText());
                break;
        }
    }

    private static string Quoted(string value) => JsonSerializer.Serialize(value, GoldenJsonContext.Default.String);
}
