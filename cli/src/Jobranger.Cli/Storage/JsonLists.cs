using System.Text;
using System.Text.Json;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Storage;

internal static class JsonLists {
    private const string separator = ", ";

    public static string Write(IEnumerable<string> items) {
        StringBuilder json = new("[");
        bool first = true;
        foreach (string item in items) {
            if (!first) {
                json.Append(separator);
            }
            first = false;
            PythonJson.AppendString(json, item);
        }
        return json.Append(']').ToString();
    }

    public static IReadOnlyList<string> Read(string? stored) {
        if (stored is null) {
            return [];
        }
        try {
            using JsonDocument document = JsonDocument.Parse(stored);
            if (document.RootElement.ValueKind != JsonValueKind.Array) {
                return [];
            }
            return [.. document.RootElement.EnumerateArray().Select(ItemText)];
        } catch (Exception error) when (error is JsonException or InvalidOperationException) {
            return [];
        }
    }

    private static string ItemText(JsonElement item) =>
        item.ValueKind == JsonValueKind.String ? item.GetString()! : item.GetRawText();
}
