using System.Text.Json;
using System.Text.Unicode;

namespace Jobranger.Cli.Features.Sources;

internal static class JsonFields {
    public static JsonDocument Parse(byte[] body) =>
        Utf8.IsValid(body) ? JsonDocument.Parse(body) : throw new InvalidDataException("json body is not UTF-8");

    public static string? Text(JsonElement parent, string name) {
        if (Field(parent, name, JsonValueKind.String) is not JsonElement value) {
            return null;
        }
        try {
            return value.GetString();
        } catch (InvalidOperationException) {
            return null;
        }
    }

    public static string? FilledText(JsonElement parent, string name) =>
        Text(parent, name) is { Length: > 0 } text ? text : null;

    public static JsonElement? Object(JsonElement parent, string name) => Field(parent, name, JsonValueKind.Object);

    public static IEnumerable<JsonElement> Objects(JsonElement parent, string name) =>
        Field(parent, name, JsonValueKind.Array) is JsonElement list
            ? list.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];

    private static JsonElement? Field(JsonElement parent, string name, JsonValueKind kind) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == kind
            ? value
            : null;
}
