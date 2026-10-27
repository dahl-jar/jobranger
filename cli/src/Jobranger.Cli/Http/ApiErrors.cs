using System.Globalization;
using System.Text.Json;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Http;

internal static class ApiErrors {
    private const int badRequest = 400;
    private const int unauthorized = 401;
    private const int forbidden = 403;
    private const int notFound = 404;
    private const int tooLarge = 413;
    private const int unprocessable = 422;
    private const int tooManyRequests = 429;
    private const int overloaded = 529;
    private const string errorField = "error";
    private const string messageField = "message";

    public static ApiErrorKind KindOf(int status, bool anthropic) => status switch {
        badRequest or tooLarge or unprocessable => ApiErrorKind.BadRequest,
        unauthorized or forbidden => ApiErrorKind.Auth,
        notFound => ApiErrorKind.NotFound,
        tooManyRequests => ApiErrorKind.RateLimit,
        overloaded when anthropic => ApiErrorKind.Overloaded,
        _ => ApiErrorKind.Server,
    };

    public static string Message(int status, string? detail) {
        string line = string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");
        return string.IsNullOrWhiteSpace(detail) ? line : $"{line}: {detail}";
    }

    public static bool Pauses(ApiErrorKind kind) => kind is ApiErrorKind.RateLimit or ApiErrorKind.Overloaded;

    public static ApiException ToException(ApiReply reply, bool anthropic, string? officialKey) {
        ApiErrorKind kind = KindOf(reply.Status, anthropic);
        string? detail = kind == ApiErrorKind.Auth || officialKey is null ? null : Detail(reply.Body, officialKey);
        return new ApiException(kind, Message(reply.Status, detail), Pauses(kind) ? reply.RetryAfter : null);
    }

    public static string? Detail(byte[] body, string key) {
        try {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                return null;
            }
            JsonElement error = root.TryGetProperty(errorField, out JsonElement nested) ? nested : root;
            return error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty(messageField, out JsonElement text)
                && text.ValueKind == JsonValueKind.String
                ? KeyGuard.Clean(text.GetString()!, key)
                : null;
        } catch (JsonException) {
            return null;
        }
    }

    public static ApiException Unreadable(string label) =>
        new(ApiErrorKind.Server, $"{label} sent a reply jobranger could not read");
}
