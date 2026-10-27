using System.Text.Json;

namespace Jobranger.Cli.Http;

internal sealed record ApiReply(int Status, byte[] Body, TimeSpan? RetryAfter) {
    private const int firstSuccess = 200;
    private const int lastSuccess = 299;

    public bool IsSuccess => Status is >= firstSuccess and <= lastSuccess;

    public static JsonElement? Field(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value : null;

    public T Read<T>(string label, Func<JsonElement, T> read) {
        try {
            using JsonDocument document = JsonDocument.Parse(Body);
            return read(document.RootElement);
        } catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) {
            throw ApiErrors.Unreadable(label);
        }
    }
}
